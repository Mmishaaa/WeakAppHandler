using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence;

/// <summary>
/// Read-only view onto the alerting schema Notification owns and migrates (TASK-032). Same
/// no-migrations rule as <see cref="GatewayReadDbContext"/>, applied to a second service's tables -
/// kept as its own DbContext rather than extra DbSets on <see cref="GatewayReadDbContext"/> so a
/// schema boundary owned by a different service stays a type boundary in code too, the same way
/// Processor/Notification/Auth stay three separate contexts against one physical database.
/// </summary>
public sealed class GatewayAlertingReadDbContext(DbContextOptions<GatewayAlertingReadDbContext> options)
    : DbContext(options)
{
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();

    public DbSet<AlertRuleEntity> AlertRules => Set<AlertRuleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AlertEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AlertRuleEntityConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}
