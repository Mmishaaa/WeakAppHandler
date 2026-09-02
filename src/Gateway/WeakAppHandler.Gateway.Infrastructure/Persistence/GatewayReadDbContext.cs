using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Configurations;
using WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence;

/// <summary>
/// Read-only view onto the core schema Processor owns and migrates. No migrations live here and
/// none must ever be added: this context exists to SELECT, never to CREATE/ALTER/INSERT/UPDATE
/// (PRD's Gateway ADR - a Gateway.Domain layer was deliberately not created for the same reason).
/// </summary>
public sealed class GatewayReadDbContext(DbContextOptions<GatewayReadDbContext> options) : DbContext(options)
{
    public DbSet<MeterEntity> Meters => Set<MeterEntity>();

    public DbSet<ReadingEntity> Readings => Set<ReadingEntity>();

    public DbSet<MeterCurrentStateEntity> MeterCurrentStates => Set<MeterCurrentStateEntity>();

    public DbSet<AggregationBucketRowEntity> AggregationBucketRows => Set<AggregationBucketRowEntity>();

    // Applied one by one rather than via ApplyConfigurationsFromAssembly: since TASK-032 added a
    // second read-only DbContext (GatewayAlertingReadDbContext) to this same assembly, an
    // assembly-wide scan would pull that context's alerting configurations into this context's
    // model too (and vice versa) - EF Core's scan is not aware of which DbContext it was invoked
    // from, only of the assembly it was pointed at.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MeterEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ReadingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MeterCurrentStateEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AggregationBucketRowEntityConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}
