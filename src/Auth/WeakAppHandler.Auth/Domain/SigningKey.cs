namespace WeakAppHandler.Auth.Domain;

/// <summary>
/// The Auth Service's own RS256 signing key, persisted so its JWKS <c>kid</c> stays stable across
/// restarts instead of invalidating every outstanding token on process recycle.
/// </summary>
public sealed class SigningKey
{
    public required string KeyId { get; init; }

    public required byte[] PrivateKeyPkcs8 { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
