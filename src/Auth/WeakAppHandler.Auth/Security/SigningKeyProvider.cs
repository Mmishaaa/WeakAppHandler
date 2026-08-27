using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace WeakAppHandler.Auth.Security;

/// <summary>
/// Holds the process-lifetime RSA signing key once <see cref="SigningKeyInitializer"/> has loaded
/// or created it. Registered as a singleton so every request reuses the same key material instead
/// of re-reading it from the database.
/// </summary>
public sealed class SigningKeyProvider
{
    private RSA? _rsa;
    private string? _keyId;

    public bool IsInitialized => _rsa is not null;

    public RSA Rsa => _rsa ?? throw new InvalidOperationException(
        "Signing key has not been initialized. SigningKeyInitializer.EnsureInitializedAsync must run at startup.");

    public string KeyId => _keyId ?? throw new InvalidOperationException(
        "Signing key has not been initialized. SigningKeyInitializer.EnsureInitializedAsync must run at startup.");

    public void Initialize(RSA rsa, string keyId)
    {
        _rsa = rsa;
        _keyId = keyId;
    }

    public RsaSecurityKey CreateSecurityKey() => new(Rsa) { KeyId = KeyId };
}
