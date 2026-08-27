using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WeakAppHandler.Auth.Persistence;
using WeakAppHandler.Auth.Persistence.Configurations;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Auth.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AuthEndpointsTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Test host not initialized.");

    public async Task InitializeAsync()
    {
        await using var migrationContext = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseNpgsql(fixture.Postgres.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options);
        await migrationContext.Database.MigrateAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.UseSetting("ConnectionStrings:Auth", fixture.Postgres.ConnectionString));

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Login_WithValidViewerCredentials_ReturnsAccessTokenAndSetsHttpOnlyRefreshCookie()
    {
        var response = await LoginAsync(AuthSeedData.ViewerEmail, AuthSeedData.ViewerPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Viewer", body.GetProperty("role").GetString());

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("refresh_token=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await LoginAsync(AuthSeedData.ViewerEmail, "not-the-real-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwks_PublishesKeyThatValidatesRealAccessTokenSignature()
    {
        var loginResponse = await LoginAsync(AuthSeedData.AdminEmail, AuthSeedData.AdminPassword);
        var accessToken = (await loginResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;

        var keySet = new JsonWebKeySet(await Client.GetStringAsync("/.well-known/jwks.json"));

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            accessToken,
            new TokenValidationParameters
            {
                ValidIssuer = "weakapphandler-auth",
                ValidAudience = "weakapphandler",
                IssuerSigningKeys = keySet.GetSigningKeys(),
            },
            out _);

        Assert.True(principal.IsInRole("Admin"));
    }

    [Fact]
    public async Task Jwks_TokenSignedWithAForeignKey_FailsValidation()
    {
        var keySet = new JsonWebKeySet(await Client.GetStringAsync("/.well-known/jwks.json"));

        using var foreignRsa = RSA.Create(2048);
        var foreignToken = new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Issuer = "weakapphandler-auth",
            Audience = "weakapphandler",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(foreignRsa), SecurityAlgorithms.RsaSha256),
        });

        var handler = new JwtSecurityTokenHandler();
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(
            foreignToken,
            new TokenValidationParameters
            {
                ValidIssuer = "weakapphandler-auth",
                ValidAudience = "weakapphandler",
                IssuerSigningKeys = keySet.GetSigningKeys(),
            },
            out _));
    }

    [Fact]
    public async Task Refresh_WithValidCookie_IssuesNewAccessTokenAndRevokesOldRefreshToken()
    {
        var loginResponse = await LoginAsync(AuthSeedData.ViewerEmail, AuthSeedData.ViewerPassword);
        var originalToken = ExtractCookieValue(loginResponse);

        var refreshResponse = await PostWithCookieAsync("/refresh", originalToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var rotatedToken = ExtractCookieValue(refreshResponse);
        Assert.NotEqual(originalToken, rotatedToken);

        var reuseResponse = await PostWithCookieAsync("/refresh", originalToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/refresh");
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ExtractCookieValue(HttpResponseMessage response)
    {
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        const string prefix = "refresh_token=";
        var start = setCookie.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var end = setCookie.IndexOf(';', start);
        return setCookie[start..(end < 0 ? setCookie.Length : end)];
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password)
        => Client.PostAsJsonAsync("/login", new { email, password });

    private async Task<HttpResponseMessage> PostWithCookieAsync(string path, string cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"refresh_token={cookieValue}");
        return await Client.SendAsync(request);
    }
}
