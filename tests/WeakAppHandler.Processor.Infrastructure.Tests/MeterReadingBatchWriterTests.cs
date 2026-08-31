using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-019's three acceptance criteria, against a real PostgreSQL container: an unknown
/// (location, meter_type) auto-registers a meter, a later successful poll for the same meter
/// advances <c>last_seen_at</c>, and an <c>air_quality</c> payload becomes three <c>readings</c> rows
/// sharing one <c>observed_at</c>.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class MeterReadingBatchWriterTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task WriteAsync_UnknownLocationAndMeterType_AutoRegistersAMeterRow()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
        var writer = new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance);

        var batchId = Guid.NewGuid();
        var observedAt = TruncateToMicroseconds(DateTimeOffset.UtcNow);
        var envelope = new MeterReadingEnvelope(
            "auto-register-attic", "motion", """{"motionDetected":true}""", "hash-1");

        await CreateBatchAsync(context, batchId, observedAt);
        await writer.WriteAsync(batchId, observedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        var meter = await context.Meters.AsNoTracking().SingleAsync(
            m => m.Location == "auto-register-attic" && m.MeterType == "motion");

        Assert.Equal(observedAt, meter.FirstSeenAt);
        Assert.Equal(observedAt, meter.LastSeenAt);
    }

    [Fact]
    public async Task WriteAsync_SecondSuccessfulPollForSameMeter_AdvancesLastSeenAtWithoutDuplicatingTheMeter()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var firstObservedAt = TruncateToMicroseconds(DateTimeOffset.UtcNow);
        var secondObservedAt = firstObservedAt.AddSeconds(10);
        var envelope = new MeterReadingEnvelope(
            "auto-register-kitchen", "energy", """{"energy":220.72}""", "hash-2");

        // A fresh writer per poll, matching production: IReadingBatchWriter is resolved scoped, once
        // per message delivery, so its per-batch meter cache must not survive across polls either.
        var firstBatchId = Guid.NewGuid();
        await CreateBatchAsync(context, firstBatchId, firstObservedAt);
        await new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance)
            .WriteAsync(firstBatchId, firstObservedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        var secondBatchId = Guid.NewGuid();
        await CreateBatchAsync(context, secondBatchId, secondObservedAt);
        await new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance)
            .WriteAsync(secondBatchId, secondObservedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        var meters = await context.Meters.AsNoTracking()
            .Where(m => m.Location == "auto-register-kitchen" && m.MeterType == "energy")
            .ToListAsync();

        var meter = Assert.Single(meters);
        Assert.Equal(firstObservedAt, meter.FirstSeenAt);
        Assert.Equal(secondObservedAt, meter.LastSeenAt);
    }

    [Fact]
    public async Task WriteAsync_AirQualityPayload_WritesThreeReadingRowsSharingObservedAt()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
        var writer = new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance);

        var batchId = Guid.NewGuid();
        var observedAt = TruncateToMicroseconds(DateTimeOffset.UtcNow);
        var envelope = new MeterReadingEnvelope(
            "auto-register-corridor",
            "air_quality",
            """{"co2":727,"pm25":42,"humidity":47}""",
            "hash-3");

        await CreateBatchAsync(context, batchId, observedAt);
        var rowCount = await writer.WriteAsync(batchId, observedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Equal(3, rowCount);

        var readings = await context.Readings.AsNoTracking()
            .Where(r => r.BatchId == batchId)
            .ToListAsync();

        Assert.Equal(3, readings.Count);
        Assert.All(readings, r => Assert.Equal(observedAt, r.ObservedAt));
        Assert.Contains(readings, r => r.MetricCode == "co2" && r.ValueNumeric == 727m);
        Assert.Contains(readings, r => r.MetricCode == "pm25" && r.ValueNumeric == 42m);
        Assert.Contains(readings, r => r.MetricCode == "humidity" && r.ValueNumeric == 47m);
    }

    // Postgres timestamptz has microsecond resolution; DateTimeOffset.UtcNow can carry a sub-tick
    // remainder Npgsql silently drops on write, which would otherwise fail an exact-equality assert.
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % (TimeSpan.TicksPerMillisecond / 1000)), value.Offset);

    // readings.batch_id is a foreign key into ingest_batches; in production IngestionRecorder
    // always inserts this row before the writer runs (it owns the transaction the writer shares).
    private static async Task CreateBatchAsync(CoreDbContext context, Guid batchId, DateTimeOffset fetchedAt)
    {
        context.IngestBatches.Add(new IngestBatch
        {
            Id = batchId,
            FetchedAt = fetchedAt,
            Outcome = IngestBatchOutcome.Success,
            DurationMs = 120,
            ReadingCount = 1,
        });

        await context.SaveChangesAsync();
    }
}
