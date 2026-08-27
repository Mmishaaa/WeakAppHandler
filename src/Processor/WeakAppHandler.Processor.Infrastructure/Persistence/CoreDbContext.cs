using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<Meter> Meters => Set<Meter>();

    public DbSet<Metric> Metrics => Set<Metric>();

    public DbSet<Reading> Readings => Set<Reading>();

    public DbSet<MeterCurrentState> MeterCurrentStates => Set<MeterCurrentState>();

    public DbSet<IngestBatch> IngestBatches => Set<IngestBatch>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    public DbSet<ReadingHourly> ReadingsHourly => Set<ReadingHourly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
    }
}
