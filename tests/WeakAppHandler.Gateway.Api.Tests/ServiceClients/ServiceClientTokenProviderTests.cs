using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeakAppHandler.Gateway.Api.ServiceClients;

namespace WeakAppHandler.Gateway.Api.Tests.ServiceClients;

/// <summary>
/// TASK-026's caching/refresh behaviour in isolation, no real Auth Service involved: a fake handler
/// stands in for <c>/token</c>, and a <see cref="FakeTimeProvider"/> makes "the cached token is about
/// to expire" a deterministic condition to test rather than something only a slow test could observe.
/// </summary>
public sealed class ServiceClientTokenProviderTests
{
    private static readonly Uri TokenUri = new("http://auth.invalid/token");

    [Fact]
    public async Task GetAccessTokenAsync_FirstCall_MintsATokenFromTheConfiguredEndpoint()
    {
        var handler = new ScriptedTokenHandler(TokenResponse("token-1", expiresInSeconds: 300));
        using var provider = CreateProvider(handler, out _);

        var token = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", token);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CalledAgainBeforeExpiry_ReusesTheCachedTokenWithoutMintingAgain()
    {
        var handler = new ScriptedTokenHandler(TokenResponse("token-1", expiresInSeconds: 300));
        using var provider = CreateProvider(handler, out _);

        await provider.GetAccessTokenAsync(CancellationToken.None);
        var second = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CalledAfterTheCachedTokenExpires_MintsAFreshOne()
    {
        var handler = new ScriptedTokenHandler(
            TokenResponse("token-1", expiresInSeconds: 300),
            TokenResponse("token-2", expiresInSeconds: 300));
        using var provider = CreateProvider(handler, out var timeProvider);

        var first = await provider.GetAccessTokenAsync(CancellationToken.None);

        // Past the 300s lifetime and the provider's own refresh margin, so the cache must be
        // considered expired rather than merely close to it.
        timeProvider.Advance(TimeSpan.FromSeconds(301));

        var second = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", first);
        Assert.Equal("token-2", second);
        Assert.Equal(2, handler.CallCount);
    }

    private static ServiceClientTokenProvider CreateProvider(HttpMessageHandler handler, out FakeTimeProvider timeProvider)
    {
        timeProvider = new FakeTimeProvider();

        var services = new ServiceCollection();
        services.AddHttpClient(ServiceClientTokenProvider.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var options = Options.Create(new ServiceClientOptions
        {
            TokenUri = TokenUri,
            ClientId = "gateway-ingestor",
            ClientSecret = "gateway-ingestor-secret-CHANGE-ME",
        });

        return new ServiceClientTokenProvider(provider.GetRequiredService<IHttpClientFactory>(), options, timeProvider);
    }

    private static Func<HttpResponseMessage> TokenResponse(string accessToken, int expiresInSeconds) => () =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"accessToken":"{{accessToken}}","tokenType":"Bearer","expiresInSeconds":{{expiresInSeconds}},"scope":"ingestion:admin"}""",
                Encoding.UTF8,
                "application/json"),
        };

    /// <summary>Stands in for the Auth Service's <c>/token</c> endpoint: one canned response per
    /// call, repeating the last once the script is exhausted.</summary>
    private sealed class ScriptedTokenHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _index;

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var factory = responses[Math.Min(_index, responses.Length - 1)];
            _index++;
            return Task.FromResult(factory());
        }
    }
}
