using System.Security.Cryptography;

namespace WeakAppHandler.Auth.Security;

/// <summary>
/// PBKDF2-SHA256 password hashing (PRD §7.1: "Argon2id or PBKDF2"). Stored format is
/// "{iterations}.{saltBase64}.{keyBase64}" so <see cref="Verify"/> can re-derive with the exact
/// parameters a hash was created with, even if <see cref="DefaultIterations"/> changes later.
/// </summary>
public static class Pbkdf2PasswordHasher
{
    public const int DefaultIterations = 100_000;

    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;

    public static string Hash(string password)
        => Hash(password, RandomNumberGenerator.GetBytes(SaltSizeBytes), DefaultIterations);

    /// <summary>
    /// Hashes with an explicit salt/iteration count. Real (non-seed) callers should use
    /// <see cref="Hash(string)"/> instead - this overload exists so seed data (see AuthSeedData)
    /// can produce a value that is identical every time EF evaluates the model, which HasData
    /// requires; a random salt there would make every `dotnet ef migrations add` invocation see a
    /// "model changed" diff purely from re-hashing the same seed password.
    /// </summary>
    public static string Hash(string password, byte[] salt, int iterations)
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedKey = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
