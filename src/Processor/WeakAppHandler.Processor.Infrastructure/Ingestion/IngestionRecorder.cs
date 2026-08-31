using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Writes what one ingestion message says into the database, exactly once (PRD §6 F3). Both
/// messages of a poll attempt land here: <see cref="IngestAttemptRecorded"/> carries the outcome of
/// the attempt and <see cref="ReadingsIngested"/> carries the readings a successful one returned.
/// </summary>
/// <remarks>
/// <para>
/// Idempotency is the <c>processed_messages</c> ledger. The message id is inserted in the same
/// transaction as the message's effects, so a redelivery — which RabbitMQ guarantees can happen —
/// either finds the id and writes nothing, or loses the race on the ledger's primary key and is
/// reported as the redelivery it is. Either way exactly one set of rows exists per message id.
/// </para>
/// <para>
/// Both messages of one attempt write the same <c>ingest_batches</c> row, and the two queues can be
/// consumed in any order. The attempt record is authoritative and overwrites whatever it finds; the
/// readings consumer only fills a row in when none exists yet, because the readings message knows
/// nothing about the HTTP status or the final outcome of the attempt that produced it.
/// </para>
/// </remarks>
public sealed partial class IngestionRecorder(
    CoreDbContext dbContext,
    IReadingBatchWriter readingBatchWriter,
    TimeProvider timeProvider,
    ILogger<IngestionRecorder> logger)
{
    public Task<IngestionRecordResult> RecordAttemptAsync(
        IngestAttemptRecorded attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return RecordAsync(
            attempt.MessageId,
            token => ApplyAttemptAsync(attempt, token),
            cancellationToken);
    }

    public Task<IngestionRecordResult> RecordReadingsAsync(
        ReadingsIngested readings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readings);

        return RecordAsync(
            readings.MessageId,
            token => ApplyReadingsAsync(readings, token),
            cancellationToken);
    }

    /// <summary>
    /// The message outcome and the persisted one are separate enums on purpose: the contract belongs
    /// to the wire and the domain one to the <c>ingest_batches.outcome</c> column, and a new outcome
    /// on either side has to be reconciled here rather than silently cast across.
    /// </summary>
    private static IngestBatchOutcome MapOutcome(IngestOutcome outcome) => outcome switch
    {
        IngestOutcome.Success => IngestBatchOutcome.Success,
        IngestOutcome.HttpError => IngestBatchOutcome.HttpError,
        IngestOutcome.Timeout => IngestBatchOutcome.Timeout,
        IngestOutcome.Corrupted => IngestBatchOutcome.Corrupted,
        IngestOutcome.RateLimited => IngestBatchOutcome.RateLimited,
        IngestOutcome.Unauthorized => IngestBatchOutcome.Unauthorized,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown ingest outcome."),
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Recorded ingest attempt {Outcome} for batch {BatchId} with {ReadingCount} readings")]
    private static partial void LogAttemptRecorded(
        ILogger logger,
        IngestBatchOutcome outcome,
        Guid batchId,
        int readingCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Stored {ReadingRowCount} reading rows from {MeterCount} meters for batch {BatchId}")]
    private static partial void LogReadingsRecorded(
        ILogger logger,
        int readingRowCount,
        int meterCount,
        Guid batchId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Message {MessageId} was already processed; discarding the redelivery")]
    private static partial void LogDuplicateDetected(ILogger logger, Guid messageId);

    private async Task<IngestionRecordResult> RecordAsync(
        Guid messageId,
        Func<CancellationToken, Task> persist,
        CancellationToken cancellationToken)
    {
        var alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(m => m.MessageId == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyProcessed)
        {
            LogDuplicateDetected(logger, messageId);
            return IngestionRecordResult.Duplicate;
        }

        var committed = false;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            dbContext.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                ProcessedAt = timeProvider.GetUtcNow(),
            });

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await persist(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            committed = true;

            return IngestionRecordResult.Recorded;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Rolled back before anything else is asked of the connection: Postgres refuses every
            // further command on an aborted transaction until it is ended.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

            var processedConcurrently = await dbContext.ProcessedMessages
                .AnyAsync(m => m.MessageId == messageId, cancellationToken)
                .ConfigureAwait(false);

            if (processedConcurrently)
            {
                // Two deliveries of the same message overlapped and this one lost the ledger's
                // primary key. That is a redelivery, not a fault: dead-lettering it would be wrong.
                LogDuplicateDetected(logger, messageId);
                return IngestionRecordResult.Duplicate;
            }

            // Any other unique violation is the ingest_batches primary key, i.e. the two consumers
            // of one batch inserting its row at the same instant. Letting it out puts the message
            // back through the endpoint's retry policy, and the retry finds the row and updates it.
            throw;
        }
        finally
        {
            if (!committed)
            {
                // Entities that failed to save stay tracked, and MassTransit may retry this message
                // on the same scope — a second Add of the same key would then throw on the tracker
                // rather than on the database, and the retry would never be able to succeed.
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task ApplyAttemptAsync(IngestAttemptRecorded attempt, CancellationToken cancellationToken)
    {
        var outcome = MapOutcome(attempt.Outcome);

        var batch = await dbContext.IngestBatches
            .FirstOrDefaultAsync(b => b.Id == attempt.BatchId, cancellationToken)
            .ConfigureAwait(false);

        if (batch is null)
        {
            dbContext.IngestBatches.Add(new IngestBatch
            {
                Id = attempt.BatchId,
                FetchedAt = attempt.FetchedAt,
                Outcome = outcome,
                HttpStatus = attempt.HttpStatus,
                DurationMs = attempt.DurationMs,
                ReadingCount = attempt.ReadingCount,
                ErrorMessage = attempt.ErrorMessage,
            });
        }
        else
        {
            // The attempt record is the authoritative account of the poll, so it overwrites the
            // provisional row the readings consumer inserted when it arrived first.
            batch.Outcome = outcome;
            batch.HttpStatus = attempt.HttpStatus;
            batch.DurationMs = attempt.DurationMs;
            batch.ReadingCount = attempt.ReadingCount;
            batch.ErrorMessage = attempt.ErrorMessage;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogAttemptRecorded(logger, outcome, attempt.BatchId, attempt.ReadingCount);
    }

    private async Task ApplyReadingsAsync(ReadingsIngested readings, CancellationToken cancellationToken)
    {
        var batchExists = await dbContext.IngestBatches
            .AnyAsync(b => b.Id == readings.BatchId, cancellationToken)
            .ConfigureAwait(false);

        if (!batchExists)
        {
            // Provisional: a readings message only exists for a successful poll, but its HTTP status
            // and the duration of the attempt are only known to the attempt record, which overwrites
            // this row when it arrives. reading_count is the meter count the Ingestor reported, the
            // same figure the attempt record carries, so the two never disagree.
            dbContext.IngestBatches.Add(new IngestBatch
            {
                Id = readings.BatchId,
                FetchedAt = readings.FetchedAt,
                Outcome = IngestBatchOutcome.Success,
                DurationMs = readings.SourceLatencyMs,
                ReadingCount = readings.Readings.Count,
            });
        }

        // Saved before the readings are written so the row they reference by foreign key is already
        // there whatever the writer does. Still inside the transaction: if the writer fails, this
        // insert goes back out with it.
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var rowCount = await readingBatchWriter
            .WriteAsync(readings.BatchId, readings.FetchedAt, readings.Readings, cancellationToken)
            .ConfigureAwait(false);

        // The writer shares this context and is not required to flush what it tracked.
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogReadingsRecorded(logger, rowCount, readings.Readings.Count, readings.BatchId);
    }
}
