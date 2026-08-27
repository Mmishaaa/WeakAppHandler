using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ServiceClient> ServiceClients => Set<ServiceClient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
