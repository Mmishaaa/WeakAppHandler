using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Distinct from EF Core's shared "__EFMigrationsHistory" default: Auth/Processor/Notification
    /// share one physical database (production and Testcontainers-backed tests alike), and their
    /// writer roles otherwise collide over ownership of that one table (TASK-047).
    /// </summary>
    public const string MigrationsHistoryTableName = "__ef_migrations_history_processor";

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
