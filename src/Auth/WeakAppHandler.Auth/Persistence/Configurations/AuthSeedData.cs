using WeakAppHandler.Auth.Domain;
using WeakAppHandler.Auth.Security;

namespace WeakAppHandler.Auth.Persistence.Configurations;

/// <summary>
/// Seed users/service client documented in README.md. Salts below are fixed (not random) so
/// <see cref="Pbkdf2PasswordHasher.Hash(string, byte[], int)"/> produces the exact same literal
/// value every time EF evaluates this HasData - see that method's doc comment for why a random
/// salt would break migration generation.
/// </summary>
public static class AuthSeedData
{
    public const string ViewerEmail = "viewer@weakapphandler.local";
    public const string ViewerPassword = "Viewer#12345";

    public const string AdminEmail = "admin@weakapphandler.local";
    public const string AdminPassword = "Admin#12345";

    public const string ServiceClientId = "gateway-ingestor";
    public const string ServiceClientSecret = "gateway-ingestor-secret-CHANGE-ME";
    public const string ServiceClientScope = "ingestion:admin";

    private static readonly Guid ViewerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset SeedCreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly byte[] ViewerSeedSalt = Convert.FromHexString("4C3F1E7A9B2D4E5F6A7B8C9D0E1F2A3B");
    private static readonly byte[] AdminSeedSalt = Convert.FromHexString("9F8E7D6C5B4A392817065F4E3D2C1B0A");
    private static readonly byte[] ServiceClientSeedSalt = Convert.FromHexString("1A2B3C4D5E6F708192A3B4C5D6E7F809");

    public static IReadOnlyList<User> Users { get; } =
    [
        new User
        {
            Id = ViewerUserId,
            Email = ViewerEmail,
            DisplayName = "Demo Viewer",
            PasswordHash = Pbkdf2PasswordHasher.Hash(ViewerPassword, ViewerSeedSalt, Pbkdf2PasswordHasher.DefaultIterations),
            Role = AuthRoles.Viewer,
            CreatedAt = SeedCreatedAt,
        },
        new User
        {
            Id = AdminUserId,
            Email = AdminEmail,
            DisplayName = "Demo Admin",
            PasswordHash = Pbkdf2PasswordHasher.Hash(AdminPassword, AdminSeedSalt, Pbkdf2PasswordHasher.DefaultIterations),
            Role = AuthRoles.Admin,
            CreatedAt = SeedCreatedAt,
        },
    ];

    public static IReadOnlyList<ServiceClient> ServiceClients { get; } =
    [
        new ServiceClient
        {
            ClientId = ServiceClientId,
            ClientSecretHash = Pbkdf2PasswordHasher.Hash(ServiceClientSecret, ServiceClientSeedSalt, Pbkdf2PasswordHasher.DefaultIterations),
            Scopes = [ServiceClientScope],
        },
    ];
}
