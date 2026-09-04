extern alias AuthApi;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthApi::WeakAppHandler.Auth.Persistence;
using AuthApi::WeakAppHandler.Auth.Persistence.Configurations;
using AuthApi::WeakAppHandler.Auth.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// Builds the real Gateway.Api host (same Program.cs the service actually runs) against a real
/// PostgreSQL container, matching the precedent set by Auth.Tests' <c>WebApplicationFactory&lt;Program&gt;</c>
/// usage rather than hand-mirroring the wiring.
/// </summary>
/// <remarks>
/// TASK-042 put <c>[Authorize]</c> on the Gateway's GraphQL root types and REST controllers, so a
/// bare host is no longer enough: this now runs the real Auth Service alongside the Gateway and
/// mints real user tokens from it - the same arrangement <see cref="GatewayAdminProxyHost"/> and
/// Notification's <c>AlertRulesAdminHost</c> already use - rather than hand-forging a JWT or
/// switching the host onto the dev bypass, neither of which would exercise the JWKS validation a
/// deployment actually runs.
/// </remarks>
internal sealed class GatewayApiFactory : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _gatewayFactory;
    private readonly WebApplicationFactory<JwtTokenService> _authFactory;
    private readonly HttpClient _authClient;

    private GatewayApiFactory(
        WebApplicationFactory<Program> gatewayFactory,
        WebApplicationFactory<JwtTokenService> authFactory,
        HttpClient authClient,
        string viewerToken,
        string adminToken)
    {
        _gatewayFactory = gatewayFactory;
        _authFactory = authFactory;
        _authClient = authClient;
        ViewerToken = viewerToken;
        AdminToken = adminToken;
    }

    /// <summary>A real user token for the seeded viewer, minted by the Auth Service's own /login -
    /// enough for every Viewer-policy read, and the right negative case for an admin-only one.</summary>
    public string ViewerToken { get; }

    /// <summary>A real user token for the seeded admin, same provenance as <see cref="ViewerToken"/>.</summary>
    public string AdminToken { get; }

    /// <summary>The Gateway host's own services, for tests that inspect the host rather than call it
    /// over HTTP (the GraphQL schema snapshot resolves its request executor from here).</summary>
    public IServiceProvider Services => _gatewayFactory.Services;

    /// <param name="connectionString">Postgres connection string the Gateway's read-only contexts bind to.</param>
    /// <param name="rabbitMqConnectionString">
    /// TASK-032: since Program.cs now unconditionally starts a MassTransit bus (the
    /// onReadingStored subscription's receive endpoint), every test host needs a real broker to
    /// connect to - the shared fixture's, not an unconfigured default of localhost:5672/guest that
    /// would either hang retrying against nothing or, worse, quietly bind against a real broker
    /// left running on a developer's machine from `docker compose up`.
    /// </param>
    /// <param name="environment">
    /// Defaults to Development, matching every test written before TASK-025 needed to care - most of
    /// what this factory builds (GraphQL query behaviour) does not depend on it. TASK-025's own tests
    /// pass "Production" to reach the introspection-disabled configuration a real deployment runs
    /// under, since <c>IHostEnvironment.IsDevelopment()</c> is the only thing that switches it.
    /// </param>
    public static async Task<GatewayApiFactory> CreateAsync(
        string connectionString,
        string rabbitMqConnectionString,
        string environment = "Development")
    {
        await MigrateAuthDatabaseAsync(connectionString);

        var authFactory = new WebApplicationFactory<JwtTokenService>().WithWebHostBuilder(
            builder => builder.UseSetting("ConnectionStrings:Auth", connectionString));
        var authClient = authFactory.CreateClient();

        var amqp = new Uri(rabbitMqConnectionString);

        var gatewayFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("ConnectionStrings:Gateway", connectionString)
            .UseSetting("RabbitMq:Host", amqp.Host)
            .UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:VirtualHost", "/")
            .UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username)
            .UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password)
            .UseSetting("Auth:Issuer", JwtTokenService.Issuer)
            .UseSetting("Auth:Audience", JwtTokenService.Audience)

            // Placeholder: nothing ever dials it, because the named JWKS client's transport below is
            // redirected into the in-process Auth TestServer, which has no real port to name here.
            .UseSetting("Auth:JwksUri", "http://localhost/.well-known/jwks.json")
            .UseSetting("Auth:RequireHttpsMetadata", "false")
            .ConfigureTestServices(services => services
                .AddHttpClient(ServiceAuthenticationExtensions.JwksHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(authFactory.Server.CreateHandler)));

        return new GatewayApiFactory(
            gatewayFactory,
            authFactory,
            authClient,
            await RequestTokenAsync(authClient, AuthSeedData.ViewerEmail, AuthSeedData.ViewerPassword),
            await RequestTokenAsync(authClient, AuthSeedData.AdminEmail, AuthSeedData.AdminPassword));
    }

    /// <summary>An anonymous client, for the negative paths and for the surfaces that carry no
    /// authorization requirement of their own (the OpenAPI document, Swagger UI, health checks).</summary>
    public HttpClient CreateClient() => _gatewayFactory.CreateClient();

    /// <summary>The same client with <paramref name="token"/> attached as a Bearer credential.</summary>
    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _gatewayFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        await _gatewayFactory.DisposeAsync();
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
