using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Retention;

/// <summary>
/// TASK-048's retention job. Runs in three steps, in one transaction:
/// <list type="number">
/// <item>Roll up every <c>readings</c> row older than the cutoff into <c>readings_hourly</c> (one
/// row per meter/metric/hour-bucket, numeric readings only - there is no aggregate representation
/// for a boolean reading in this schema).</item>
/// <item>Delete <c>ingest_batches</c> rows older than the cutoff. <c>readings.batch_id</c> is a
/// required FK configured with <see cref="DeleteBehavior.Cascade"/> (see the
/// <c>PipelineSchema</c> migration), so this single delete removes exactly the raw readings just
/// rolled up in step 1 - a batch's <c>fetched_at</c> and its readings' <c>observed_at</c> are always
/// close together (one poll, one moment in time), so nothing is deleted from a batch whose readings
/// have not also aged out.</item>
/// <item>Delete <c>processed_messages</c> rows older than the cutoff - the idempotency ledger has no
/// FK to anything and would otherwise grow forever.</item>
/// </list>
/// The rollup INSERT uses <c>ON CONFLICT DO NOTHING</c> against <c>readings_hourly</c>'s own unique
/// index so a re-run (a second manual trigger, or a retry after a mid-run failure) is a no-op for
/// buckets already written rather than a constraint-violation crash or a corrupted double-count.
/// </summary>
public sealed partial class RetentionJob(
    CoreDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<RetentionOptions> options,
    ILogger<RetentionJob> logger) : IRetentionJob
{
    public async Task<RetentionResult> RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - options.Value.Window;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var hourlyBucketsWritten = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO readings_hourly
                 (meter_id, metric_code, bucket_start, value_avg, value_min, value_max, value_sum, reading_count)
             SELECT
                 r.meter_id,
                 r.metric_code,
                 date_trunc('hour', r.observed_at),
                 AVG(r.value_numeric),
                 MIN(r.value_numeric),
                 MAX(r.value_numeric),
                 SUM(r.value_numeric),
                 COUNT(*)
             FROM readings r
             WHERE r.observed_at < {cutoff} AND r.value_numeric IS NOT NULL
             GROUP BY r.meter_id, r.metric_code, date_trunc('hour', r.observed_at)
             ON CONFLICT (meter_id, metric_code, bucket_start) DO NOTHING
             """,
            cancellationToken).ConfigureAwait(false);

        var ingestBatchesDeleted = await dbContext.IngestBatches
            .Where(b => b.FetchedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var processedMessagesDeleted = await dbContext.ProcessedMessages
            .Where(m => m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var result = new RetentionResult(
            cutoff, hourlyBucketsWritten, ingestBatchesDeleted, processedMessagesDeleted);

        LogCompleted(
            logger,
            result.CutoffUtc,
            result.HourlyBucketsWritten,
            result.IngestBatchesDeleted,
            result.ProcessedMessagesDeleted);

        return result;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Retention run complete (cutoff {CutoffUtc}): {HourlyBucketsWritten} hourly bucket(s) written, "
            + "{IngestBatchesDeleted} ingest batch(es) deleted, {ProcessedMessagesDeleted} processed message(s) deleted")]
    private static partial void LogCompleted(
        ILogger logger,
        DateTimeOffset cutoffUtc,
        int hourlyBucketsWritten,
        int ingestBatchesDeleted,
        int processedMessagesDeleted);
}
