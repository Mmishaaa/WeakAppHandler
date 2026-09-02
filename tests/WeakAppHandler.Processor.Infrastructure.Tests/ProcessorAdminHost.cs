extern alias AuthApi;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthApi::WeakAppHandler.Auth.Persistence;
using AuthApi::WeakAppHandler.Auth.Persistence.Configurations;
using AuthApi::WeakAppHandler.Auth.Security;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// The Processor's admin API as it really runs (TASK-021), against the real Auth Service that
/// guards it: Program.cs's own wiring, a real broker, a real database, and real machine/viewer
/// tokens minted by the Auth Service's own grants. Mirrors
/// <c>WeakAppHandler.Ingestor.Tests.IngestorAdminHost</c>, which established this shape for the
/// Ingestor's own admin surface (TASK-017).
/// </summary>
internal sealed class ProcessorAdminHost : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    private readonly WebApplicationFactory<Program> _processorFactory;
    private readonly WebApplicationFactory<JwtTokenService> _authFactory;
    private readonly HttpClient _authClient;

    private ProcessorAdminHost(
        WebApplicationFactory<Program> processorFactory,
        WebApplicationFactory<JwtTokenService> authFactory,
        HttpClient client,
        HttpClient authClient,
        string machineToken,
        string viewerToken,
        ConsumeCounter consumed)
    {
        _processorFactory = processorFactory;
        _authFactory = authFactory;
        _authClient = authClient;
        Client = client;
        MachineToken = machineToken;
        ViewerToken = viewerToken;
        Consumed = consumed;
    }

    /// <summary>
    /// How many times the bus has finished consuming each message id, so a test can wait for a
    /// publish to actually land before asserting on the stats it should have moved.
    /// </summary>
    public ConsumeCounter Consumed { get; }

    /// <summary>Talks to the Processor. Carries no credentials of its own; each call attaches its own.</summary>
    public HttpClient Client { get; }

    /// <summary>A real client-credentials token carrying the <c>ingestion:admin</c> scope.</summary>
    public string MachineToken { get; }

    /// <summary>
    /// A real user token for the seeded viewer. Carries role claims and no scope at all, which is
    /// what makes it the right negative case: the admin API must be closed to browser users even
    /// when they authenticate perfectly well.
    /// </summary>
    public string ViewerToken { get; }

    public IBus Bus => _processorFactory.Services.GetRequiredService<IBus>();

    public static async Task<ProcessorAdminHost> StartAsync(IntegrationTestFixture fixture, string virtualHost)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await MigrateAuthDatabaseAsync(fixture.Postgres.ConnectionString);
        await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var authFactory = new WebApplicationFactory<JwtTokenService>().WithWebHostBuilder(
            builder => builder.UseSetting("ConnectionStrings:Auth", fixture.Postgres.ConnectionString));

        var authClient = authFactory.CreateClient();
        var amqp = new Uri(fixture.RabbitMq.ConnectionString);

        var processorFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Processor", fixture.Postgres.ConnectionString);
            builder.UseSetting("RabbitMq:Host", amqp.Host);
            builder.UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("RabbitMq:VirtualHost", virtualHost);
            builder.UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username);
            builder.UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password);

            // The same settings a service gets from appsettings.json, pointed at the Auth Service
            // this test is hosting. Authority stays unset for the production reason: the Auth
            // Service publishes a JWKS document but no OpenID discovery document.
            builder.UseSetting("Auth:Issuer", JwtTokenService.Issuer);
            builder.UseSetting("Auth:Audience", JwtTokenService.Audience);
            builder.UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json");
            builder.UseSetting("Auth:RequireHttpsMetadata", "false");

            builder.ConfigureTestServices(services =>
            {
                // MassTransit connects in the background by default, so the host would start before
                // the queues exist.
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = StartStopTimeout;
                    options.StopTimeout = StartStopTimeout;
                });

                services.AddHttpClient(ServiceAuthenticationExtensions.JwksHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler);
            });
        });

        var client = processorFactory.CreateClient();

        // Starts the host (and with it the bus) before anything is timed.
        using var startup = await client.GetAsync("/health/live");

        var consumed = new ConsumeCounter();
        processorFactory.Services.GetRequiredService<IBusControl>().ConnectConsumeObserver(consumed);

        return new ProcessorAdminHost(
            processorFactory,
            authFactory,
            client,
            authClient,
            await RequestMachineTokenAsync(authClient),
            await RequestViewerTokenAsync(authClient),
            consumed);
    }

    public Task<HttpResponseMessage> GetStatsAsync(string? token) =>
        SendAsync(HttpMethod.Get, "/api/v1/processing/stats", token);

    /// <summary>Stats body, asserting a 200 so a failure reads as the status code rather than a JSON error.</summary>
    public async Task<JsonElement> ReadStatsAsync(string token)
    {
        using var response = await GetStatsAsync(token);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _authClient.Dispose();
        await _processorFactory.DisposeAsync();
        await _authFactory.DisposeAsync();
    }

    private static async Task MigrateAuthDatabaseAsync(string connectionString)
    {
        await using var context = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options);

        await context.Database.MigrateAsync();
    }

    private static async Task<string> RequestMachineTokenAsync(HttpClient authClient)
    {
        using var response = await authClient.PostAsJsonAsync(
            "/token",
            new { clientId = AuthSeedData.ServiceClientId, clientSecret = AuthSeedData.ServiceClientSecret });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> RequestViewerTokenAsync(HttpClient authClient)
    {
        using var response = await authClient.PostAsJsonAsync(
            "/login",
            new { email = AuthSeedData.ViewerEmail, password = AuthSeedData.ViewerPassword });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token)
    {
        using var request = new HttpRequestMessage(method, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await Client.SendAsync(request);
    }
}
