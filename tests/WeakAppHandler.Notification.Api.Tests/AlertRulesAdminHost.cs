extern alias AuthApi;

using System.Globalization;
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

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// The Notification service's TASK-030 admin REST surface as it really runs, guarded by the real
/// Auth Service. Mirrors <see cref="AlertsHubHost"/> (TASK-031)'s two-hosts-talking-for-real
/// arrangement almost exactly, extended with a real admin token alongside the viewer one: the admin
/// surface's own negative case needs a token that authenticates fine but carries the wrong role,
/// which only a real viewer login (not just an admin one) can prove.
/// </summary>
internal sealed class AlertRulesAdminHost : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    private readonly WebApplicationFactory<Program> _notificationFactory;
    private readonly WebApplicationFactory<JwtTokenService> _authFactory;
    private readonly HttpClient _authClient;

    private AlertRulesAdminHost(
        WebApplicationFactory<Program> notificationFactory,
        WebApplicationFactory<JwtTokenService> authFactory,
        HttpClient client,
        HttpClient authClient,
        string adminToken,
        string viewerToken)
    {
        _notificationFactory = notificationFactory;
        _authFactory = authFactory;
        _authClient = authClient;
        Client = client;
        AdminToken = adminToken;
        ViewerToken = viewerToken;
    }

    /// <summary>Talks to the Notification service. Carries no credentials of its own.</summary>
    public HttpClient Client { get; }

    /// <summary>A real user token for the seeded admin, minted by the Auth Service's own /login.</summary>
    public string AdminToken { get; }

    /// <summary>
    /// A real user token for the seeded viewer - authenticates fine but carries the wrong role,
    /// which is what makes it the right negative case for an admin-only surface.
    /// </summary>
    public string ViewerToken { get; }

    public static async Task<AlertRulesAdminHost> StartAsync(IntegrationTestFixture fixture, string virtualHost)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await MigrateAuthDatabaseAsync(fixture.Postgres.ConnectionString);
        await AlertingDatabase.MigrateAsync(fixture.Postgres.ConnectionString);

        var authFactory = new WebApplicationFactory<JwtTokenService>().WithWebHostBuilder(
            builder => builder.UseSetting("ConnectionStrings:Auth", fixture.Postgres.ConnectionString));

        var authClient = authFactory.CreateClient();
        var amqp = new Uri(fixture.RabbitMq.ConnectionString);

        var notificationFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Notification", fixture.Postgres.ConnectionString)
            .UseSetting("RabbitMq:Host", amqp.Host)
            .UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:VirtualHost", virtualHost)
            .UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username)
            .UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password)
            .UseSetting("Auth:Issuer", JwtTokenService.Issuer)
            .UseSetting("Auth:Audience", JwtTokenService.Audience)
            .UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json")
            .UseSetting("Auth:RequireHttpsMetadata", "false")
            .ConfigureServices(services =>
            {
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = StartStopTimeout;
                    options.StopTimeout = StartStopTimeout;
                });

                services.AddHttpClient(ServiceAuthenticationExtensions.JwksHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler);
            }));

        var client = notificationFactory.CreateClient();

        // Starts the host (and with it the bus) before anything is timed.
        using var startup = await client.GetAsync("/health/live");

        return new AlertRulesAdminHost(
            notificationFactory,
            authFactory,
            client,
            authClient,
            await RequestTokenAsync(authClient, AuthSeedData.AdminEmail, AuthSeedData.AdminPassword),
            await RequestTokenAsync(authClient, AuthSeedData.ViewerEmail, AuthSeedData.ViewerPassword));
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _authClient.Dispose();
        await _notificationFactory.DisposeAsync();
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

    private static async Task<string> RequestTokenAsync(HttpClient authClient, string email, string password)
    {
        using var response = await authClient.PostAsJsonAsync("/login", new { email, password });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }
}
