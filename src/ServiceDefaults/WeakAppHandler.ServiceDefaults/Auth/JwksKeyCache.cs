using Microsoft.IdentityModel.Tokens;

namespace WeakAppHandler.ServiceDefaults.Auth;

/// <summary>
/// Fetches and caches the signing keys published at a JWKS endpoint, so token validation does
/// not need to re-fetch the key set on every request. Refreshes the cache on a fixed interval
/// rather than on every cache miss, since a rotated/unknown key id is expected to be rare.
/// </summary>
/// <remarks>
/// Takes the factory rather than an <see cref="HttpClient"/> because this cache is a singleton and
/// lives for the life of the process: a client held that long never picks up a DNS change, which is
/// exactly the failure mode <see cref="IHttpClientFactory"/> exists to prevent. A client is created
/// per refresh instead, which is cheap — the handler underneath it is pooled.
/// </remarks>
public sealed class JwksKeyCache(
    IHttpClientFactory httpClientFactory,
    string jwksUri,
    TimeSpan? cacheDuration = null) : IDisposable
{
    private readonly TimeSpan _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(15);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private JsonWebKeySet? _keySet;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public IEnumerable<SecurityKey> GetSigningKeys()
    {
        if (_keySet is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _keySet.GetSigningKeys();
        }

        _refreshLock.Wait();
        try
        {
            if (_keySet is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _keySet.GetSigningKeys();
            }

            using var httpClient = httpClientFactory.CreateClient(
                ServiceAuthenticationExtensions.JwksHttpClientName);

            var json = httpClient.GetStringAsync(jwksUri).GetAwaiter().GetResult();
            _keySet = new JsonWebKeySet(json);
            _expiresAt = DateTimeOffset.UtcNow.Add(_cacheDuration);

            return _keySet.GetSigningKeys();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }
}
