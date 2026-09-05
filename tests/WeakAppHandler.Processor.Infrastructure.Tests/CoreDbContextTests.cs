using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class CoreDbContextTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_SeedsMetricReferenceData()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var codes = await context.Metrics.Select(m => m.Code).ToListAsync();

        Assert.Equal(5, codes.Count);
        Assert.Contains("co2", codes);
        Assert.Contains("motion_detected", codes);
    }

    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_PersistsPipelineSchemaRoundTrip()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var meter = new Meter
        {
            Id = Guid.NewGuid(),
            Location = "Garage",
            MeterType = "air_quality",
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        };
        context.Meters.Add(meter);

        var batch = new IngestBatch
        {
            Id = Guid.NewGuid(),
            FetchedAt = DateTimeOffset.UtcNow,
            Outcome = IngestBatchOutcome.Success,
            HttpStatus = 200,
            DurationMs = 42,
            ReadingCount = 1,
        };
        context.IngestBatches.Add(batch);

        var reading = new Reading
        {
            MeterId = meter.Id,
            MetricCode = "co2",
            ObservedAt = DateTimeOffset.UtcNow,
            ValueNumeric = 512.5m,
            IsChanged = true,
            BatchId = batch.Id,
        };
        context.Readings.Add(reading);

        var processedMessage = new ProcessedMessage
        {
            MessageId = Guid.NewGuid(),
            ProcessedAt = DateTimeOffset.UtcNow,
        };
        context.ProcessedMessages.Add(processedMessage);

        var hourlyBucket = new ReadingHourly
        {
            MeterId = meter.Id,
            MetricCode = "co2",
            BucketStart = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            ValueAvg = 512.5m,
            ValueMin = 500m,
            ValueMax = 525m,
            ValueSum = 1537.5m,
            ReadingCount = 3,
        };
        context.ReadingsHourly.Add(hourlyBucket);

        await context.SaveChangesAsync();

        var reloadedBatch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == batch.Id);
        Assert.Equal(IngestBatchOutcome.Success, reloadedBatch.Outcome);

        var reloadedReading = await context.Readings.AsNoTracking().SingleAsync(r => r.BatchId == batch.Id);
        Assert.Equal(batch.Id, reloadedReading.BatchId);

        var reloadedMessage = await context.ProcessedMessages.AsNoTracking()
            .SingleAsync(m => m.MessageId == processedMessage.MessageId);

        // Postgres timestamptz has microsecond precision; DateTimeOffset ticks are 100ns, so a
        // round trip through the database can lose the last decimal digit.
        Assert.Equal(processedMessage.ProcessedAt, reloadedMessage.ProcessedAt, TimeSpan.FromMilliseconds(1));

        var reloadedBucket = await context.ReadingsHourly.AsNoTracking()
            .SingleAsync(h => h.MeterId == meter.Id && h.MetricCode == "co2");
        Assert.Equal(3, reloadedBucket.ReadingCount);
    }

    [Fact]
    public void Fixture_StartsRabbitMqContainerAlongsidePostgres()
    {
        Assert.False(string.IsNullOrWhiteSpace(fixture.RabbitMq.ConnectionString));
    }

    private CoreDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(
                fixture.Postgres.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTableName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CoreDbContext(options);
    }
}
