using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Auth.Persistence;
using WeakAppHandler.Auth.Persistence.Configurations;
using WeakAppHandler.Auth.Security;
using WeakAppHandler.Ingestor.Polling;
using WeakAppHandler.Ingestor.WeakApp;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// The Ingestor's admin API as it really runs (TASK-017), against the real Auth Service that guards
/// it: Program.cs's own wiring, a real broker, the real <see cref="WeakAppClient"/> behind its real
/// resilience pipeline, and real machine tokens minted by the client-credentials grant. Only WeakApp
/// itself is a stand-in, because a flaky third-party server is exactly what the outcomes under test
/// describe.
/// </summary>
/// <remarks>
/// The two hosts talk to each other for real: the Ingestor fetches the JWKS over HTTP from the Auth
/// Service through <see cref="TestServer.CreateHandler"/>, so signature validation resolves a key the
/// Auth Service actually published rather than one the test handed it. Only the transport underneath
/// that fetch is in-memory — two in-process test servers have no port to reach each other on.
/// </remarks>
internal sealed class IngestorAdminHost : IAsyncDisposable
{
    /// <summary>
    /// Long enough that the timer polls once at startup and then stays out of the way while a test
    /// asserts. The scheduled poll is deliberately not suppressed: /status is supposed to report on
    /// the loop's own polling, not only on polls a test triggered.
    /// </summary>
    public const int DefaultPollingIntervalSeconds = 300;

    private const int TotalTimeoutSeconds = 5;

    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StatusPollInterval = TimeSpan.FromMilliseconds(50);

    private readonly WebApplicationFactory<IngestionPoller> _ingestorFactory;
    private readonly WebApplicationFactory<JwtTokenService> _authFactory;
    private readonly HttpClient _authClient;

    private IngestorAdminHost(
        WebApplicationFactory<IngestionPoller> ingestorFactory,
        WebApplicationFactory<JwtTokenService> authFactory,
        HttpClient client,
        HttpClient authClient,
        string machineToken,
        string viewerToken)
    {
        _ingestorFactory = ingestorFactory;
        _authFactory = authFactory;
        _authClient = authClient;
        Client = client;
        MachineToken = machineToken;
        ViewerToken = viewerToken;
    }

    /// <summary>Talks to the Ingestor. Carries no credentials of its own; each call attaches its own.</summary>
    public HttpClient Client { get; }

    /// <summary>A real client-credentials token carrying the <c>ingestion:admin</c> scope.</summary>
    public string MachineToken { get; }

    /// <summary>
    /// A real user token for the seeded viewer. Carries role claims and no scope at all, which is
    /// what makes it the right negative case: the admin API must be closed to browser users even
    /// when they authenticate perfectly well.
    /// </summary>
    public string ViewerToken { get; }

    public static async Task<IngestorAdminHost> StartAsync(
        IntegrationTestFixture fixture,
        string virtualHost,
        HttpMessageHandler weakApp,
        int pollingIntervalSeconds = DefaultPollingIntervalSeconds)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await MigrateAuthDatabaseAsync(fixture.Postgres.ConnectionString);

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
            builder.UseSetting(
                "WeakApp:PollingIntervalSeconds",
                pollingIntervalSeconds.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("WeakApp:AttemptTimeoutSeconds", "2");
            builder.UseSetting("WeakApp:TotalTimeoutSeconds", TotalTimeoutSeconds.ToString(CultureInfo.InvariantCulture));

            // One retry and a two-failure threshold, so a single failing poll is enough to open the
            // breaker: the test asserting /status reports it open should not have to wait out four
            // rounds of exponential backoff to get there.
            builder.UseSetting("WeakApp:MaxRetryAttempts", "1");
            builder.UseSetting("WeakApp:CircuitBreakerMinimumThroughput", "2");

            // The same settings a service gets from appsettings.json, pointed at the Auth Service
            // this test is hosting. Authority stays unset for the production reason: the Auth Service
            // publishes a JWKS document but no OpenID discovery document.
            builder.UseSetting("Auth:Issuer", JwtTokenService.Issuer);
            builder.UseSetting("Auth:Audience", JwtTokenService.Audience);
            builder.UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json");
            builder.UseSetting("Auth:RequireHttpsMetadata", "false");

            builder.ConfigureTestServices(services =>
            {
                // MassTransit connects in the background by default, so the host would start — and
                // the loop would publish its first poll — before the bus was up.
                services.Configure<MassTransitHostOptions>(options =>
                {
                    options.WaitUntilStarted = true;
                    options.StartTimeout = StartStopTimeout;
                    options.StopTimeout = StartStopTimeout;
                });

                // Only the primary handlers are swapped. The JWKS client keeps going through
                // IHttpClientFactory, and the WeakApp client keeps its real resilience handler, so
                // retries, timeouts and the circuit breaker all still run over these responses.
                services.AddHttpClient(ServiceAuthenticationExtensions.JwksHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler);
                services.AddHttpClient<IWeakAppClient, WeakAppClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => weakApp);
            });
        });

        var client = ingestorFactory.CreateClient();

        // Starts the host (and with it the polling loop) before anything is timed, so a later
        // "responded within a second" assertion measures the request and not one-off startup.
        using var startup = await client.GetAsync("/health/live");

        return new IngestorAdminHost(
            ingestorFactory,
            authFactory,
            client,
            authClient,
            await RequestMachineTokenAsync(authClient),
            await RequestViewerTokenAsync(authClient));
    }

    public Task<HttpResponseMessage> GetStatusAsync(string? token) =>
        SendAsync(HttpMethod.Get, "/api/v1/ingestion/status", token);

    public Task<HttpResponseMessage> TriggerAsync(string? token) =>
        SendAsync(HttpMethod.Post, "/api/v1/ingestion/trigger", token);

    public Task<HttpResponseMessage> PutConfigAsync(string? token, int pollingIntervalSeconds) =>
        SendAsync(
            HttpMethod.Put,
            "/api/v1/ingestion/config",
            token,
            JsonContent.Create(new { pollingIntervalSeconds }));

    /// <summary>Status body, asserting a 200 so a failure reads as the status code rather than a JSON error.</summary>
    public async Task<JsonElement> ReadStatusAsync(string token)
    {
        using var response = await GetStatusAsync(token);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Polls /status until it satisfies <paramref name="predicate"/>. The loop's own poll runs
    /// concurrently with the test, so anything it is expected to record has to be waited for rather
    /// than assumed to have landed already.
    /// </summary>
    public async Task<JsonElement> WaitForStatusAsync(
        string token,
        Func<JsonElement, bool> predicate,
        TimeSpan timeout,
        string description)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var deadline = DateTime.UtcNow + timeout;
        JsonElement status;

        while (true)
        {
            status = await ReadStatusAsync(token);

            if (predicate(status))
            {
                return status;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Status never satisfied '{description}' within {timeout}. Last status: {status}");
            }

            await Task.Delay(StatusPollInterval);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _authClient.Dispose();
        await _ingestorFactory.DisposeAsync();
        await _authFactory.DisposeAsync();
    }

    private static async Task MigrateAuthDatabaseAsync(string connectionString)
    {
        await using var context = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable(AuthDbContext.MigrationsHistoryTableName))
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

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? token,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await Client.SendAsync(request);
    }
}
