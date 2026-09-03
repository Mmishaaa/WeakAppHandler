using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WeakAppHandler.Gateway.Api.ServiceClients;

/// <summary>
/// Mints and caches the machine token this Gateway presents to the Ingestor's/Processor's admin
/// APIs (TASK-026), via the same client-credentials grant the Auth Service's <c>/token</c> endpoint
/// already serves. A singleton, since the token is valid for every proxied request regardless of
/// which browser client triggered it - there is nothing per-request to key a cache on.
/// </summary>
public sealed class ServiceClientTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceClientOptions> options,
    TimeProvider timeProvider) : IDisposable
{
    /// <summary>Name of the <see cref="IHttpClientFactory"/> client used to call <c>/token</c>.</summary>
    public const string HttpClientName = "weakapphandler-service-client-token";

    /// <summary>
    /// Refreshed this far ahead of the token's own expiry, so a request that starts an instant
    /// before expiry never presents a token the Auth Service is about to reject mid-flight.
    /// </summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();

    // Serialises the mint call itself (a network round trip), separate from the Lock above which
    // only ever guards a synchronous field read/write - concurrent callers that all miss the cache
    // coalesce onto one /token request instead of each minting their own.
    private readonly SemaphoreSlim _mintGate = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _mintGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have already refreshed while this one waited on the gate.
            if (TryGetCachedToken(out var stillCached))
            {
                return stillCached;
            }

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client
                .PostAsJsonAsync(
                    options.Value.TokenUri,
                    new { clientId = options.Value.ClientId, clientSecret = options.Value.ClientSecret },
                    cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content
                .ReadFromJsonAsync<JsonElement>(cancellationToken)
                .ConfigureAwait(false);
            var accessToken = body.GetProperty("accessToken").GetString()!;
            var expiresInSeconds = body.GetProperty("expiresInSeconds").GetInt32();

            lock (_gate)
            {
                _cachedToken = accessToken;
                _expiresAt = timeProvider.GetUtcNow().AddSeconds(expiresInSeconds) - RefreshMargin;
            }

            return accessToken;
        }
        finally
        {
            _mintGate.Release();
        }
    }

    public void Dispose() => _mintGate.Dispose();

    private bool TryGetCachedToken(out string token)
    {
        lock (_gate)
        {
            if (_cachedToken is { } cached && timeProvider.GetUtcNow() < _expiresAt)
            {
                token = cached;
                return true;
            }
        }

        token = string.Empty;
        return false;
    }
}
