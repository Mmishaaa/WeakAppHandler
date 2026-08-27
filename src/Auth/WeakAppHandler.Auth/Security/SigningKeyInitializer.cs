using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Auth.Domain;
using WeakAppHandler.Auth.Persistence;

namespace WeakAppHandler.Auth.Security;

/// <summary>
/// Loads the Auth Service's RS256 signing key from <c>signing_keys</c> at startup, generating and
/// persisting a new RSA-2048 key the first time the service ever runs against a given database.
/// Persisting it (rather than generating a fresh key per process) keeps the JWKS <c>kid</c> and
/// public key stable across restarts, so outstanding access tokens and other services' cached JWKS
/// responses don't go stale the moment Auth recycles.
/// </summary>
public static class SigningKeyInitializer
{
    public static async Task EnsureInitializedAsync(
        AuthDbContext db,
        SigningKeyProvider provider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (provider.IsInitialized)
        {
            return;
        }

        var existing = await db.SigningKeys
            .OrderBy(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var loadedRsa = RSA.Create();
            loadedRsa.ImportPkcs8PrivateKey(existing.PrivateKeyPkcs8, out _);
            provider.Initialize(loadedRsa, existing.KeyId);
            return;
        }

        var newRsa = RSA.Create(2048);
        var keyId = Guid.NewGuid().ToString("N");
        db.SigningKeys.Add(new SigningKey
        {
            KeyId = keyId,
            PrivateKeyPkcs8 = newRsa.ExportPkcs8PrivateKey(),
            CreatedAt = timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);

        provider.Initialize(newRsa, keyId);
    }
}
