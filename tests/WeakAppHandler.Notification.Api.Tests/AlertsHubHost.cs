extern alias AuthApi;

using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using AuthApi::WeakAppHandler.Auth.Persistence;
using AuthApi::WeakAppHandler.Auth.Persistence.Configurations;
using AuthApi::WeakAppHandler.Auth.Security;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// The Notification service exactly as Program.cs builds it (TASK-031) - the real
/// <c>SignalRAlertDispatcher</c>, a real broker, a real database - guarded by the real Auth Service.
/// Mirrors IngestorAdminHost's (TASK-017) two-hosts-talking-for-real arrangement: the Notification
/// host fetches the JWKS document over HTTP from the Auth host through <see cref="TestServer.CreateHandler"/>,
/// so signature validation resolves a key the Auth Service actually published.
/// </summary>
/// <remarks>
/// Unlike <see cref="NotificationHost"/>, this host does NOT override IAlertDispatcher: the whole
/// point of these tests is to exercise the real SignalR-backed dispatcher Program.cs wires up, not a
/// recording double.
/// </remarks>
internal sealed class AlertsHubHost : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    private readonly WebApplicationFactory<Program> _notificationFactory;
    private readonly WebApplicationFactory<JwtTokenService> _authFactory;
    private readonly HttpClient _authClient;

    private AlertsHubHost(
        WebApplicationFactory<Program> notificationFactory,
        WebApplicationFactory<JwtTokenService> authFactory,
        HttpClient authClient,
        string viewerToken)
    {
        _notificationFactory = notificationFactory;
        _authFactory = authFactory;
        _authClient = authClient;
        ViewerToken = viewerToken;
    }

    public IBus Bus => _notificationFactory.Services.GetRequiredService<IBus>();

    /// <summary>A real user token for the seeded viewer, minted by the Auth Service's own /login.</summary>
    public string ViewerToken { get; }

    public static async Task<AlertsHubHost> StartAsync(IntegrationTestFixture fixture, string virtualHost)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await MigrateAuthDatabaseAsync(fixture.Postgres.ConnectionString);

        // Applied before the host starts: the service migrates nothing itself, and a rule inserted
        // for a reading published before the tables exist would fault the message rather than skip it.
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

            // Authority stays unset for the production reason: the Auth Service publishes a JWKS
            // document but no OpenID discovery document. JwksUri is a placeholder - the actual
            // transport is swapped below to the in-memory Auth TestServer.
            .UseSetting("Auth:Issuer", JwtTokenService.Issuer)
            .UseSetting("Auth:Audience", JwtTokenService.Audience)
            .UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json")
            .UseSetting("Auth:RequireHttpsMetadata", "false")
            .ConfigureServices(services =>
            {
                // MassTransit connects in the background by default, so the host would be considered
                // started before the queues exist and a reading published straight away could be
                // routed nowhere.
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = StartStopTimeout;
                    options.StopTimeout = StartStopTimeout;
                });

                services.AddHttpClient(ServiceAuthenticationExtensions.JwksHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler);
            }));

        // WebApplicationFactory builds and starts the host lazily; touching Services is what forces
        // it, and with WaitUntilStarted set that call does not return until the bus is up.
        _ = notificationFactory.Services.GetRequiredService<IBus>();

        return new AlertsHubHost(
            notificationFactory,
            authFactory,
            authClient,
            await RequestViewerTokenAsync(authClient));
    }

    /// <summary>
    /// Builds (but does not start) a client for AlertsHub, wired through the Notification host's own
    /// in-memory TestServer. Long polling, not the default WebSockets transport: TestServer has no
    /// real socket for a WebSocket to upgrade over.
    /// </summary>
    public HubConnection CreateHubConnection(string? accessToken)
    {
        var hubUri = new Uri(_notificationFactory.Server.BaseAddress, "hubs/alerts");

        return new HubConnectionBuilder()
            .WithUrl(
                hubUri,
                HttpTransportType.LongPolling,
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _notificationFactory.Server.CreateHandler();

                    if (accessToken is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    }
                })
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
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

    private static async Task<string> RequestViewerTokenAsync(HttpClient authClient)
    {
        using var response = await authClient.PostAsJsonAsync(
            "/login",
            new { email = AuthSeedData.ViewerEmail, password = AuthSeedData.ViewerPassword });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }
}
