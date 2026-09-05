using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;
using WeakAppHandler.Processor.Infrastructure.Retention;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-048's acceptance criteria against a real PostgreSQL container: readings older than the
/// configured window are rolled up into <c>readings_hourly</c> and the raw rows deleted, the window
/// is configurable without a code change, and <c>ingest_batches</c>/<c>processed_messages</c> don't
/// grow unboundedly.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class RetentionJobTests(IntegrationTestFixture fixture)
{
    private const string MetricCode = "energy";
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunAsync_ReadingsOlderThanWindow_AreRolledUpIntoHourlyAndRawRowsDeleted()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
        var meterId = await SeedMeterAsync(context, "retention-rollup");

        var bucketStart = new DateTimeOffset(2026, 5, 1, 3, 0, 0, TimeSpan.Zero); // 45 days before Now
        var oldBatchId = await SeedBatchAsync(context, bucketStart);
        await SeedReadingAsync(context, meterId, bucketStart, oldBatchId, 10m);
        await SeedReadingAsync(context, meterId, bucketStart.AddMinutes(10), oldBatchId, 20m);

        var recentObservedAt = Now.AddDays(-1);
        var recentBatchId = await SeedBatchAsync(context, recentObservedAt);
        await SeedReadingAsync(context, meterId, recentObservedAt, recentBatchId, 99m);

        var job = CreateJob(context, windowDays: 30);

        var result = await job.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.HourlyBucketsWritten);
        Assert.Equal(1, result.IngestBatchesDeleted);

        var hourly = await context.ReadingsHourly.AsNoTracking().SingleAsync(
            h => h.MeterId == meterId && h.MetricCode == MetricCode && h.BucketStart == bucketStart);
        Assert.Equal(15m, hourly.ValueAvg);
        Assert.Equal(10m, hourly.ValueMin);
        Assert.Equal(20m, hourly.ValueMax);
        Assert.Equal(30m, hourly.ValueSum);
        Assert.Equal(2, hourly.ReadingCount);

        Assert.False(await context.IngestBatches.AnyAsync(b => b.Id == oldBatchId));
        Assert.False(await context.Readings.AnyAsync(r => r.BatchId == oldBatchId));

        // The recent batch/reading is inside the window and must survive untouched.
        Assert.True(await context.IngestBatches.AnyAsync(b => b.Id == recentBatchId));
        Assert.True(await context.Readings.AnyAsync(r => r.BatchId == recentBatchId));
    }

    [Fact]
    public async Task RunAsync_ProcessedMessagesOlderThanWindow_AreDeletedAndRecentOnesSurvive()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var oldMessageId = Guid.NewGuid();
        var recentMessageId = Guid.NewGuid();
        context.ProcessedMessages.AddRange(
            new ProcessedMessage { MessageId = oldMessageId, ProcessedAt = Now.AddDays(-45) },
            new ProcessedMessage { MessageId = recentMessageId, ProcessedAt = Now.AddDays(-1) });
        await context.SaveChangesAsync();

        var job = CreateJob(context, windowDays: 30);

        var result = await job.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.ProcessedMessagesDeleted);
        Assert.False(await context.ProcessedMessages.AnyAsync(m => m.MessageId == oldMessageId));
        Assert.True(await context.ProcessedMessages.AnyAsync(m => m.MessageId == recentMessageId));
    }

    [Fact]
    public async Task RunAsync_WithAShorterConfiguredWindow_TreatsMoreReadingsAsExpired()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
        var meterId = await SeedMeterAsync(context, "retention-shorter-window");

        // 10 days old: survives a 30-day window, but not a 7-day one - the exact TASK-048 test_step
        // 3 scenario ("change the retention window env var, repeat the test with a different
        // threshold").
        var observedAt = Now.AddDays(-10);
        var batchId = await SeedBatchAsync(context, observedAt);
        await SeedReadingAsync(context, meterId, observedAt, batchId, 42m);

        var thirtyDayJob = CreateJob(context, windowDays: 30);
        var untouchedResult = await thirtyDayJob.RunAsync(CancellationToken.None);
        Assert.Equal(0, untouchedResult.HourlyBucketsWritten);
        Assert.True(await context.IngestBatches.AnyAsync(b => b.Id == batchId));

        var sevenDayJob = CreateJob(context, windowDays: 7);
        var expiredResult = await sevenDayJob.RunAsync(CancellationToken.None);
        Assert.Equal(1, expiredResult.HourlyBucketsWritten);
        Assert.False(await context.IngestBatches.AnyAsync(b => b.Id == batchId));
    }

    [Fact]
    public async Task RunAsync_CalledTwiceForTheSameData_IsIdempotent()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
        var meterId = await SeedMeterAsync(context, "retention-idempotent");

        var observedAt = Now.AddDays(-45);
        var batchId = await SeedBatchAsync(context, observedAt);
        await SeedReadingAsync(context, meterId, observedAt, batchId, 5m);

        var job = CreateJob(context, windowDays: 30);

        var first = await job.RunAsync(CancellationToken.None);
        var second = await job.RunAsync(CancellationToken.None);

        Assert.Equal(1, first.HourlyBucketsWritten);
        Assert.Equal(0, second.HourlyBucketsWritten); // ON CONFLICT DO NOTHING, not a duplicate row

        var hourlyRowCount = await context.ReadingsHourly.CountAsync(h => h.MeterId == meterId && h.MetricCode == MetricCode);
        Assert.Equal(1, hourlyRowCount);
    }

    private static RetentionJob CreateJob(CoreDbContext context, int windowDays) => new(
        context,
        new FakeTimeProvider(Now),
        Options.Create(new RetentionOptions { WindowDays = windowDays }),
        NullLogger<RetentionJob>.Instance);

    private static async Task<Guid> SeedMeterAsync(CoreDbContext context, string location)
    {
        var meter = new Meter
        {
            Id = Guid.NewGuid(),
            Location = location,
            MeterType = "energy",
            FirstSeenAt = Now.AddDays(-90),
            LastSeenAt = Now,
        };
        context.Meters.Add(meter);
        await context.SaveChangesAsync();

        return meter.Id;
    }

    private static async Task<Guid> SeedBatchAsync(CoreDbContext context, DateTimeOffset fetchedAt)
    {
        var batch = new IngestBatch
        {
            Id = Guid.NewGuid(),
            FetchedAt = fetchedAt,
            Outcome = IngestBatchOutcome.Success,
            DurationMs = 100,
            ReadingCount = 1,
        };
        context.IngestBatches.Add(batch);
        await context.SaveChangesAsync();

        return batch.Id;
    }

    private static async Task SeedReadingAsync(
        CoreDbContext context, Guid meterId, DateTimeOffset observedAt, Guid batchId, decimal value)
    {
        context.Readings.Add(new Reading
        {
            MeterId = meterId,
            MetricCode = MetricCode,
            ObservedAt = observedAt,
            ValueNumeric = value,
            IsChanged = true,
            BatchId = batchId,
        });
        await context.SaveChangesAsync();
    }
}
