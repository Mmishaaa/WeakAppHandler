using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-018's three acceptance criteria, against a real PostgreSQL container: a redelivered message
/// id writes nothing a second time, a failed attempt still produces an <c>ingest_batches</c> row
/// with no readings, and the batch, its readings and the idempotency ledger entry are one
/// transaction. The database is what has to be checked here — an in-memory provider would happily
/// pass a test about transactions it does not implement.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class IngestionRecorderTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task RecordReadingsAsync_SameMessageDeliveredTwice_WritesOneBatchAndOneSetOfReadings()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var batchId = Guid.NewGuid();
        var message = IngestionMessages.Readings(batchId, "duplicate-readings", meterCount: 2);
        var writer = new TestReadingBatchWriter(context);
        var recorder = CreateRecorder(context, writer);

        var first = await recorder.RecordReadingsAsync(message, CancellationToken.None);
        var second = await recorder.RecordReadingsAsync(message, CancellationToken.None);

        Assert.Equal(IngestionRecordResult.Recorded, first);
        Assert.Equal(IngestionRecordResult.Duplicate, second);

        // The redelivery must not even reach the writer, or normalisation would run twice for
        // effects the ledger cannot take back.
        Assert.Equal(1, writer.Invocations);

        Assert.Equal(1, await context.IngestBatches.CountAsync(b => b.Id == batchId));
        Assert.Equal(2, await context.Readings.CountAsync(r => r.BatchId == batchId));
    }

    [Fact]
    public async Task RecordAttemptAsync_SameMessageDeliveredTwice_WritesOneBatchRow()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var batchId = Guid.NewGuid();
        var message = IngestionMessages.Attempt(batchId, IngestOutcome.Success, readingCount: 3);
        var recorder = CreateRecorder(context, new TestReadingBatchWriter(context));

        var first = await recorder.RecordAttemptAsync(message, CancellationToken.None);
        var second = await recorder.RecordAttemptAsync(message, CancellationToken.None);

        Assert.Equal(IngestionRecordResult.Recorded, first);
        Assert.Equal(IngestionRecordResult.Duplicate, second);
        Assert.Equal(1, await context.IngestBatches.CountAsync(b => b.Id == batchId));
    }

    [Theory]
    [InlineData(IngestOutcome.HttpError, 503)]
    [InlineData(IngestOutcome.Timeout, null)]
    [InlineData(IngestOutcome.Corrupted, 200)]
    [InlineData(IngestOutcome.RateLimited, 429)]
    [InlineData(IngestOutcome.Unauthorized, 401)]
    public async Task RecordAttemptAsync_FailedOutcome_WritesABatchWithNoReadings(
        IngestOutcome outcome,
        int? httpStatus)
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var batchId = Guid.NewGuid();
        var message = IngestionMessages.Attempt(
            batchId,
            outcome,
            readingCount: 0,
            httpStatus: httpStatus,
            errorMessage: "WeakApp did not answer with usable data.");

        var recorder = CreateRecorder(context, new TestReadingBatchWriter(context));

        var result = await recorder.RecordAttemptAsync(message, CancellationToken.None);

        Assert.Equal(IngestionRecordResult.Recorded, result);

        var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == batchId);

        Assert.Equal(Expected(outcome), batch.Outcome);
        Assert.Equal(httpStatus, batch.HttpStatus);
        Assert.Equal(0, batch.ReadingCount);
        Assert.Equal("WeakApp did not answer with usable data.", batch.ErrorMessage);

        // A failed poll has nothing to store, but it must still be visible as an attempt: the
        // Ingestor has no database access, so this row is the only trace the failure ever leaves.
        Assert.False(await context.Readings.AnyAsync(r => r.BatchId == batchId));
    }

    [Fact]
    public async Task RecordReadingsAsync_WriterFailsHalfway_CommitsNothingAndLeavesTheMessageRetryable()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var observedAt = DateTimeOffset.UtcNow;
        var meterId = Guid.NewGuid();

        // Committed before the batch is recorded, so the reading the failing writer inserts has a
        // meter to point at and the only thing the rollback can undo is the batch's own work.
        context.Meters.Add(new Meter
        {
            Id = meterId,
            Location = "atomicity-probe",
            MeterType = "air_quality",
            FirstSeenAt = observedAt,
            LastSeenAt = observedAt,
        });
        await context.SaveChangesAsync();

        var batchId = Guid.NewGuid();
        var message = IngestionMessages.Readings(batchId, "atomicity", meterCount: 1);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateRecorder(context, new FailingReadingBatchWriter(context, meterId))
                .RecordReadingsAsync(message, CancellationToken.None));

        Assert.Equal(FailingReadingBatchWriter.FailureMessage, failure.Message);

        // The writer really did save a reading row before throwing, so all three of these prove the
        // rollback rather than merely that nothing was attempted.
        Assert.False(await context.IngestBatches.AnyAsync(b => b.Id == batchId));
        Assert.False(await context.Readings.AnyAsync(r => r.BatchId == batchId));
        Assert.False(await context.ProcessedMessages.AnyAsync(m => m.MessageId == message.MessageId));

        // And because the ledger is clean, the redelivery MassTransit makes after the fault can
        // still succeed — a half-written ledger entry would have silently swallowed the batch.
        var retry = await CreateRecorder(context, new TestReadingBatchWriter(context))
            .RecordReadingsAsync(message, CancellationToken.None);

        Assert.Equal(IngestionRecordResult.Recorded, retry);
        Assert.Equal(1, await context.IngestBatches.CountAsync(b => b.Id == batchId));
        Assert.Equal(1, await context.Readings.CountAsync(r => r.BatchId == batchId));
    }

    [Fact]
    public async Task RecordAttemptAsync_WhenTheReadingsArrivedFirst_OverwritesTheProvisionalBatchRow()
    {
        await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

        var batchId = Guid.NewGuid();
        var recorder = CreateRecorder(context, new TestReadingBatchWriter(context));

        // The Ingestor publishes the readings before the attempt record, and the two queues are
        // consumed independently, so this is the ordinary order rather than an edge case.
        await recorder.RecordReadingsAsync(
            IngestionMessages.Readings(batchId, "ordering", meterCount: 2), CancellationToken.None);

        await recorder.RecordAttemptAsync(
            IngestionMessages.Attempt(batchId, IngestOutcome.Success, readingCount: 2, durationMs: 1234),
            CancellationToken.None);

        // SingleAsync is the assertion that matters: both messages describe one poll and must share
        // one ingest_batches row, not insert one each.
        var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == batchId);

        Assert.Equal(IngestBatchOutcome.Success, batch.Outcome);
        Assert.Equal(200, batch.HttpStatus);
        Assert.Equal(2, batch.ReadingCount);

        // The duration only the attempt record knows, proving the authoritative row won rather than
        // the provisional one the readings consumer inserted (which carried SourceLatencyMs).
        Assert.Equal(1234, batch.DurationMs);
        Assert.Equal(2, await context.Readings.CountAsync(r => r.BatchId == batchId));
    }

    private static IngestBatchOutcome Expected(IngestOutcome outcome) => outcome switch
    {
        IngestOutcome.Success => IngestBatchOutcome.Success,
        IngestOutcome.HttpError => IngestBatchOutcome.HttpError,
        IngestOutcome.Timeout => IngestBatchOutcome.Timeout,
        IngestOutcome.Corrupted => IngestBatchOutcome.Corrupted,
        IngestOutcome.RateLimited => IngestBatchOutcome.RateLimited,
        IngestOutcome.Unauthorized => IngestBatchOutcome.Unauthorized,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown ingest outcome."),
    };

    private static IngestionRecorder CreateRecorder(CoreDbContext context, IReadingBatchWriter writer) =>
        new(context, writer, TimeProvider.System, NullLogger<IngestionRecorder>.Instance);
}
