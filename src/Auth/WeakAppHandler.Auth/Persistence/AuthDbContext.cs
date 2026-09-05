using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Distinct from EF Core's shared "__EFMigrationsHistory" default: Auth/Processor/Notification
    /// share one physical database (production and Testcontainers-backed tests alike), and their
    /// writer roles otherwise collide over ownership of that one table (TASK-047).
    /// </summary>
    public const string MigrationsHistoryTableName = "__ef_migrations_history_auth";

    public DbSet<User> Users => Set<User>();

    public DbSet<ServiceClient> ServiceClients => Set<ServiceClient>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
