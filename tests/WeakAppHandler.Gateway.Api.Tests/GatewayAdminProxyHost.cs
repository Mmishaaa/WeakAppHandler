extern alias AuthApi;
extern alias IngestorApi;
extern alias ProcessorApi;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AuthApi::WeakAppHandler.Auth.Persistence;
using AuthApi::WeakAppHandler.Auth.Persistence.Configurations;
using AuthApi::WeakAppHandler.Auth.Security;
using IngestorApi::WeakAppHandler.Ingestor.Polling;
using IngestorApi::WeakAppHandler.Ingestor.WeakApp;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Gateway.Api.ServiceClients;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Auth;
using ProcessorProgram = ProcessorApi::Program;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-026's admin proxy as it really runs: the real Gateway calling out, over real HTTP, to the
/// real Ingestor and the real Processor - each guarded by the real Auth Service's client-credentials
/// grant, exactly as production does. Every downstream <see cref="HttpClient"/>'s primary handler is
/// swapped for the target's own <c>TestServer.CreateHandler</c> (only the transport is in-memory -
/// four in-process test servers have no real ports to reach each other on), the same technique
/// <c>IngestorAdminHost</c>/<c>ProcessorAdminHost</c> already use for the JWKS client.
/// </summary>
internal sealed class GatewayAdminProxyHost : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    private readonly WebApplicationFactory<Program> _gatewayFactory;
    private readonly WebApplicationFactory<IngestionPoller> _ingestorFactory;
    private readonly WebApplicationFactory<ProcessorProgram> _processorFactory;
    private readonly WebApplicationFactory<JwtTokenService> _authFactory;
    private readonly HttpClient _authClient;

    private GatewayAdminProxyHost(
        WebApplicationFactory<Program> gatewayFactory,
        WebApplicationFactory<IngestionPoller> ingestorFactory,
        WebApplicationFactory<ProcessorProgram> processorFactory,
        WebApplicationFactory<JwtTokenService> authFactory,
        HttpClient client,
        HttpClient authClient,
        HttpClient ingestorClient,
        HttpClient processorClient,
        string machineToken,
        string adminToken,
        string viewerToken)
    {
        _gatewayFactory = gatewayFactory;
        _ingestorFactory = ingestorFactory;
        _processorFactory = processorFactory;
        _authFactory = authFactory;
        _authClient = authClient;
        Client = client;
        IngestorClient = ingestorClient;
        ProcessorClient = processorClient;
        MachineToken = machineToken;
        AdminToken = adminToken;
        ViewerToken = viewerToken;
    }

    /// <summary>Talks to the Gateway. Carries no credentials of its own: since TASK-042 the admin
    /// proxy routes require an Admin token of the caller's, which each test attaches per request -
    /// the machine token the Gateway mints internally is a separate credential entirely.</summary>
    public HttpClient Client { get; }

    /// <summary>Talks to the real Ingestor directly, for comparing a direct call against the
    /// Gateway's proxied one.</summary>
    public HttpClient IngestorClient { get; }

    /// <summary>Talks to the real Processor directly, same reason as <see cref="IngestorClient"/>.</summary>
    public HttpClient ProcessorClient { get; }

    /// <summary>A real client-credentials token carrying <c>ingestion:admin</c>, for the direct calls
    /// above - the Gateway's own proxied calls mint and attach their own copy internally.</summary>
    public string MachineToken { get; }

    /// <summary>A real user token for the seeded admin, minted by the Auth Service's own /login -
    /// what TASK-042's Admin policy on the proxy routes requires.</summary>
    public string AdminToken { get; }

    /// <summary>
    /// A real user token for the seeded viewer - authenticates fine but carries the wrong role,
    /// which is what makes it the right negative case for an admin-only surface.
    /// </summary>
    public string ViewerToken { get; }

    public static async Task<GatewayAdminProxyHost> StartAsync(IntegrationTestFixture fixture, string virtualHost)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await MigrateAuthDatabaseAsync(fixture.Postgres.ConnectionString);
        await using (await ProcessorSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString))
        {
            // Migration only - the admin proxy tests read no rows, but the Processor host's own
            // health check and DbContext registration still need the schema to exist.
        }

        var authFactory = new WebApplicationFactory<JwtTokenService>().WithWebHostBuilder(
            builder => builder.UseSetting("ConnectionStrings:Auth", fixture.Postgres.ConnectionString));
        var authClient = authFactory.CreateClient();

        var amqp = new Uri(fixture.RabbitMq.ConnectionString);

        var ingestorFactory = new WebApplicationFactory<IngestionPoller>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RabbitMq:Host", amqp.Host);
            builder.UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("RabbitMq:VirtualHost", virtualHost);
            builder.UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username);
            builder.UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password);

            builder.UseSetting("WeakApp:BaseUrl", "http://weakapp.invalid");
            builder.UseSetting("WeakApp:ApiKey", "test-api-key");

            // Long enough that only the immediate startup poll runs during this test - /status must
            // report a stable snapshot for the direct-vs-proxied comparison to be meaningful.
            builder.UseSetting("WeakApp:PollingIntervalSeconds", "300");
            builder.UseSetting("WeakApp:AttemptTimeoutSeconds", "2");
            builder.UseSetting("WeakApp:TotalTimeoutSeconds", "5");

            builder.UseSetting("Auth:Issuer", JwtTokenService.Issuer);
            builder.UseSetting("Auth:Audience", JwtTokenService.Audience);
            builder.UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json");
            builder.UseSetting("Auth:RequireHttpsMetadata", "false");

            builder.ConfigureTestServices(services =>
            {
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = StartStopTimeout;
                    options.StopTimeout = StartStopTimeout;
                });

                services.AddHttpClient(ServiceAuthenticationExtensions.JwksHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler);
                services.AddHttpClient<IWeakAppClient, WeakAppClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new EmptyMetersHandler());
            });
        });

        var processorFactory = new WebApplicationFactory<ProcessorProgram>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Processor", fixture.Postgres.ConnectionString);
            builder.UseSetting("RabbitMq:Host", amqp.Host);
            builder.UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("RabbitMq:VirtualHost", virtualHost);
            builder.UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username);
            builder.UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password);

            builder.UseSetting("Auth:Issuer", JwtTokenService.Issuer);
            builder.UseSetting("Auth:Audience", JwtTokenService.Audience);
            builder.UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json");
            builder.UseSetting("Auth:RequireHttpsMetadata", "false");

            builder.ConfigureTestServices(services =>
            {
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

        var gatewayFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Gateway", fixture.Postgres.ConnectionString);
            builder.UseSetting("RabbitMq:Host", amqp.Host);
            builder.UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("RabbitMq:VirtualHost", virtualHost);
            builder.UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username);
            builder.UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password);

            // TASK-042: the Gateway now validates its own inbound tokens too, against the same real
            // Auth Service the downstream services already validate against.
            builder.UseSetting("Auth:Issuer", JwtTokenService.Issuer);
            builder.UseSetting("Auth:Audience", JwtTokenService.Audience);
            builder.UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json");
            builder.UseSetting("Auth:RequireHttpsMetadata", "false");

            builder.ConfigureTestServices(services =>
            {
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = StartStopTimeout;
                    options.StopTimeout = StartStopTimeout;
                });

                // Only the primary handlers are swapped - the token minting/base-address
                // configuration each client got from ServiceClientsServiceCollectionExtensions
                // still runs, so this proves that wiring too, not just a hand-rolled substitute.
                services.AddHttpClient(ServiceClientTokenProvider.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler);
                services.AddHttpClient(ServiceAuthenticationExtensions.JwksHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler);
                services.AddHttpClient(DownstreamServiceNames.Ingestor)
                    .ConfigurePrimaryHttpMessageHandler(ingestorFactory.Server.CreateHandler);
                services.AddHttpClient(DownstreamServiceNames.Processor)
                    .ConfigurePrimaryHttpMessageHandler(processorFactory.Server.CreateHandler);
            });
        });

        var ingestorClient = ingestorFactory.CreateClient();
        var processorClient = processorFactory.CreateClient();
        var client = gatewayFactory.CreateClient();

        // Starts every host (Ingestor's included, so its one startup poll has already happened)
        // before anything is timed or compared.
        using var ingestorStartup = await ingestorClient.GetAsync("/health/live");
        using var processorStartup = await processorClient.GetAsync("/health/live");
        using var gatewayStartup = await client.GetAsync("/health/live");

        return new GatewayAdminProxyHost(
            gatewayFactory,
            ingestorFactory,
            processorFactory,
            authFactory,
            client,
            authClient,
            ingestorClient,
            processorClient,
            await RequestMachineTokenAsync(authClient),
            await RequestUserTokenAsync(authClient, AuthSeedData.AdminEmail, AuthSeedData.AdminPassword),
            await RequestUserTokenAsync(authClient, AuthSeedData.ViewerEmail, AuthSeedData.ViewerPassword));
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        IngestorClient.Dispose();
        ProcessorClient.Dispose();
        _authClient.Dispose();
        await _gatewayFactory.DisposeAsync();
        await _ingestorFactory.DisposeAsync();
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

    /// <summary>The password grant, alongside <see cref="RequestMachineTokenAsync"/>'s
    /// client-credentials one: the proxy's own callers are browser users, not machines.</summary>
    private static async Task<string> RequestUserTokenAsync(HttpClient authClient, string email, string password)
    {
        using var response = await authClient.PostAsJsonAsync("/login", new { email, password });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    /// <summary>Stands in for WeakApp: always answers with an empty, well-formed meter list, so the
    /// Ingestor's one startup poll succeeds without depending on a real WeakApp instance.</summary>
    private sealed class EmptyMetersHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
    }
}
