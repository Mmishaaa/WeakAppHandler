using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<Meter> Meters => Set<Meter>();

    public DbSet<Metric> Metrics => Set<Metric>();

    public DbSet<Reading> Readings => Set<Reading>();

    public DbSet<MeterCurrentState> MeterCurrentStates => Set<MeterCurrentState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
    }
}
