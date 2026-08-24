using Microsoft.IdentityModel.Tokens;

namespace WeakAppHandler.ServiceDefaults.Auth;

/// <summary>
/// Fetches and caches the signing keys published at a JWKS endpoint, so token validation does
/// not need to re-fetch the key set on every request. Refreshes the cache on a fixed interval
/// rather than on every cache miss, since a rotated/unknown key id is expected to be rare.
/// </summary>
public sealed class JwksKeyCache(HttpClient httpClient, string jwksUri, TimeSpan? cacheDuration = null) : IDisposable
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
