using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GatewayReadDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}
