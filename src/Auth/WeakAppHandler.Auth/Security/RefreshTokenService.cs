using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeakAppHandler.Auth.Domain;
using WeakAppHandler.Auth.Persistence;

namespace WeakAppHandler.Auth.Security;

/// <summary>
/// Issues and redeems opaque refresh tokens. Only a SHA-256 hash of the token is ever persisted -
/// the raw value exists solely inside the httpOnly cookie, so a database read alone can never
/// reconstruct a usable token. Redeeming rotates the token (the old one is revoked and a new one
/// issued) so a captured, already-used refresh cookie cannot be replayed.
/// </summary>
public sealed class RefreshTokenService(AuthDbContext db, TimeProvider timeProvider, IOptions<AuthTokenOptions> options)
{
    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rawToken = GenerateRawToken();
        var now = timeProvider.GetUtcNow();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(options.Value.RefreshTokenLifetime),
        });
        await db.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<RefreshResult?> RedeemAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var entity = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (entity is null || entity.RevokedAt is not null || entity.ExpiresAt <= now)
        {
            return null;
        }

        var user = await db.Users.FindAsync([entity.UserId], cancellationToken);
        if (user is null)
        {
            return null;
        }

        entity.RevokedAt = now;

        var newRawToken = GenerateRawToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(newRawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(options.Value.RefreshTokenLifetime),
        });
        await db.SaveChangesAsync(cancellationToken);

        return new RefreshResult(user, newRawToken);
    }

    private static string GenerateRawToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
