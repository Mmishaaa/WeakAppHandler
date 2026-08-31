using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// Seeds the core schema through Processor's own <see cref="CoreDbContext"/> - the service that
/// owns and migrates it - so these tests exercise the Gateway against exactly the tables/columns
/// production writes, not a hand-rolled schema that could silently drift from it.
/// </summary>
internal static class ProcessorSchemaSeed
{
    public static async Task<CoreDbContext> CreateMigratedContextAsync(string connectionString)
    {
        var context = new CoreDbContext(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options);

        await context.Database.MigrateAsync();

        return context;
    }

    /// <summary>One meter with <paramref name="count"/> readings of <paramref name="metricCode"/>, one minute apart, newest at <paramref name="anchor"/>.</summary>
    public static async Task<Guid> SeedMeterWithReadingsAsync(
        CoreDbContext context,
        string location,
        string meterType,
        string metricCode,
        int count,
        DateTimeOffset anchor)
    {
        var meter = new Meter
        {
            Id = Guid.NewGuid(),
            Location = location,
            MeterType = meterType,
            FirstSeenAt = anchor.AddMinutes(-count),
            LastSeenAt = anchor,
        };
        context.Meters.Add(meter);

        for (var i = 0; i < count; i++)
        {
            var observedAt = anchor.AddMinutes(-i);
            var batch = new IngestBatch
            {
                Id = Guid.NewGuid(),
                FetchedAt = observedAt,
                Outcome = IngestBatchOutcome.Success,
                DurationMs = 50,
                ReadingCount = 1,
            };
            context.IngestBatches.Add(batch);

            context.Readings.Add(new Reading
            {
                MeterId = meter.Id,
                MetricCode = metricCode,
                ObservedAt = observedAt,
                ValueNumeric = 100 + i,
                IsChanged = true,
                BatchId = batch.Id,
            });
        }

        await context.SaveChangesAsync();

        return meter.Id;
    }

    /// <summary>Adds <paramref name="count"/> more readings to an already-seeded meter, e.g. to place some outside a test's time-range filter without violating the (location, meterType) natural key.</summary>
    public static async Task SeedReadingsForExistingMeterAsync(
        CoreDbContext context,
        Guid meterId,
        string metricCode,
        int count,
        DateTimeOffset anchor)
    {
        for (var i = 0; i < count; i++)
        {
            var observedAt = anchor.AddMinutes(-i);
            var batch = new IngestBatch
            {
                Id = Guid.NewGuid(),
                FetchedAt = observedAt,
                Outcome = IngestBatchOutcome.Success,
                DurationMs = 50,
                ReadingCount = 1,
            };
            context.IngestBatches.Add(batch);

            context.Readings.Add(new Reading
            {
                MeterId = meterId,
                MetricCode = metricCode,
                ObservedAt = observedAt,
                ValueNumeric = 100 + i,
                IsChanged = true,
                BatchId = batch.Id,
            });
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedCurrentValueAsync(
        CoreDbContext context,
        Guid meterId,
        string metricCode,
        decimal valueNumeric,
        decimal? previousValueNumeric,
        DateTimeOffset observedAt)
    {
        context.MeterCurrentStates.Add(new MeterCurrentState
        {
            MeterId = meterId,
            MetricCode = metricCode,
            ValueNumeric = valueNumeric,
            PreviousValueNumeric = previousValueNumeric,
            ObservedAt = observedAt,
            ChangedAt = observedAt,
        });

        await context.SaveChangesAsync();
    }
}
