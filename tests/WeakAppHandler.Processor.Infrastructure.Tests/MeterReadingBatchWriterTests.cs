using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-019 and TASK-020's acceptance criteria, against a real PostgreSQL container: TASK-019 —
/// an unknown (location, meter_type) auto-registers a meter, a later successful poll for the same
/// meter advances <c>last_seen_at</c>, and an <c>air_quality</c> payload becomes three
/// <c>readings</c> rows sharing one <c>observed_at</c>. TASK-020 — a repeated identical value leaves
/// <c>readings.is_changed</c> false while still advancing <c>meter_current_state.observed_at</c>, a
/// changed value flips it true and carries the prior value forward, and the returned
/// <see cref="ReadingStored"/> events reflect exactly what was written without the caller needing to
/// query the database.
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
        var events = await writer.WriteAsync(batchId, observedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Equal(3, events.Count);
        Assert.All(events, e => Assert.True(e.IsChanged));
        Assert.All(events, e => Assert.Null(e.PreviousValue));

        var readings = await context.Readings.AsNoTracking()
            .Where(r => r.BatchId == batchId)
            .ToListAsync();

        Assert.Equal(3, readings.Count);
        Assert.All(readings, r => Assert.Equal(observedAt, r.ObservedAt));
        Assert.Contains(readings, r => r.MetricCode == "co2" && r.ValueNumeric == 727m);
        Assert.Contains(readings, r => r.MetricCode == "pm25" && r.ValueNumeric == 42m);
        Assert.Contains(readings, r => r.MetricCode == "humidity" && r.ValueNumeric == 47m);
    }

    [Fact]
    public async Task WriteAsync_FirstReadingForANewMeterAndMetric_IsChangedWithNoPreviousValue()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
        var writer = new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance);

        var batchId = Guid.NewGuid();
        var observedAt = TruncateToMicroseconds(DateTimeOffset.UtcNow);
        var envelope = new MeterReadingEnvelope(
            "current-state-den", "energy", """{"energy":100.50}""", "hash-cs-1");

        await CreateBatchAsync(context, batchId, observedAt);
        var events = await writer.WriteAsync(batchId, observedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        var stored = Assert.Single(events);
        Assert.Equal("energy", stored.MetricCode);
        Assert.Equal(100.50, stored.Value.Numeric);
        Assert.Null(stored.PreviousValue);
        Assert.True(stored.IsChanged);
        Assert.Equal(observedAt, stored.ObservedAt);

        var state = await context.MeterCurrentStates.AsNoTracking()
            .SingleAsync(s => s.MeterId == stored.MeterId && s.MetricCode == "energy");

        Assert.Equal(100.50m, state.ValueNumeric);
        Assert.Null(state.PreviousValueNumeric);
        Assert.Equal(observedAt, state.ObservedAt);
        Assert.Equal(observedAt, state.ChangedAt);
    }

    [Fact]
    public async Task WriteAsync_SameValueOnSecondPoll_IsUnchangedButStillAdvancesObservedAt()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var firstObservedAt = TruncateToMicroseconds(DateTimeOffset.UtcNow);
        var secondObservedAt = firstObservedAt.AddSeconds(10);
        var envelope = new MeterReadingEnvelope(
            "current-state-hall", "energy", """{"energy":300.25}""", "hash-cs-2");

        var firstBatchId = Guid.NewGuid();
        await CreateBatchAsync(context, firstBatchId, firstObservedAt);
        var firstEvents = await new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance)
            .WriteAsync(firstBatchId, firstObservedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        var secondBatchId = Guid.NewGuid();
        await CreateBatchAsync(context, secondBatchId, secondObservedAt);
        var secondEvents = await new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance)
            .WriteAsync(secondBatchId, secondObservedAt, [envelope], CancellationToken.None);
        await context.SaveChangesAsync();

        var meterId = firstEvents.Single().MeterId;

        var secondReading = await context.Readings.AsNoTracking().SingleAsync(r => r.BatchId == secondBatchId);
        Assert.False(secondReading.IsChanged);

        var secondStored = Assert.Single(secondEvents);
        Assert.False(secondStored.IsChanged);
        Assert.Equal(300.25, secondStored.PreviousValue!.Numeric);
        Assert.Equal(300.25, secondStored.Value.Numeric);

        var state = await context.MeterCurrentStates.AsNoTracking()
            .SingleAsync(s => s.MeterId == meterId && s.MetricCode == "energy");

        // observed_at advances even though nothing changed; changed_at stays at the first poll.
        Assert.Equal(secondObservedAt, state.ObservedAt);
        Assert.Equal(firstObservedAt, state.ChangedAt);
    }

    [Fact]
    public async Task WriteAsync_ChangedValueOnSecondPoll_IsChangedAndCarriesThePriorValueAsPrevious()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var firstObservedAt = TruncateToMicroseconds(DateTimeOffset.UtcNow);
        var secondObservedAt = firstObservedAt.AddSeconds(10);

        var firstBatchId = Guid.NewGuid();
        await CreateBatchAsync(context, firstBatchId, firstObservedAt);
        await new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance).WriteAsync(
            firstBatchId,
            firstObservedAt,
            [new MeterReadingEnvelope("current-state-garage", "motion", """{"motionDetected":false}""", "hash-cs-3")],
            CancellationToken.None);
        await context.SaveChangesAsync();

        var secondBatchId = Guid.NewGuid();
        await CreateBatchAsync(context, secondBatchId, secondObservedAt);
        var secondEvents = await new MeterReadingBatchWriter(context, NullLogger<MeterReadingBatchWriter>.Instance)
            .WriteAsync(
                secondBatchId,
                secondObservedAt,
                [new MeterReadingEnvelope("current-state-garage", "motion", """{"motionDetected":true}""", "hash-cs-4")],
                CancellationToken.None);
        await context.SaveChangesAsync();

        var stored = Assert.Single(secondEvents);
        Assert.True(stored.IsChanged);
        Assert.Equal(false, stored.PreviousValue!.Boolean);
        Assert.Equal(true, stored.Value.Boolean);

        var secondReading = await context.Readings.AsNoTracking().SingleAsync(r => r.BatchId == secondBatchId);
        Assert.True(secondReading.IsChanged);

        var state = await context.MeterCurrentStates.AsNoTracking()
            .SingleAsync(s => s.MeterId == stored.MeterId && s.MetricCode == "motion_detected");

        Assert.Equal(secondObservedAt, state.ChangedAt);
        Assert.Equal(false, state.PreviousValueBool);
        Assert.Equal(true, state.ValueBool);
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
