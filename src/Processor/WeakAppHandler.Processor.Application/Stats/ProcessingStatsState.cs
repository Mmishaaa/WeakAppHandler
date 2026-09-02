using WeakAppHandler.Processor.Application.Ingestion;

namespace WeakAppHandler.Processor.Application.Stats;

/// <summary>
/// What the Processor knows about its own message handling, in memory (TASK-021): how many messages
/// it has recorded, deduplicated or dead-lettered since this process started. Deliberately not
/// persisted, the same way the Ingestor's <c>IngestionRuntimeState</c> is not — this is operational
/// state about a running process, not a record of what was ingested (that is <c>ingest_batches</c>,
/// already exposed by the consumers this state is fed from).
/// </summary>
/// <remarks>
/// A singleton written from every ingestion consumer and every fault consumer, and read by the
/// admin controller, so every member is guarded.
/// </remarks>
public sealed class ProcessingStatsState
{
    private readonly Lock _gate = new();

    private int _processed;
    private int _deduplicated;
    private int _deadLettered;

    /// <summary>
    /// Records what the recorder reported for one consumed message: newly written, or a redelivery
    /// of one already recorded.
    /// </summary>
    public void RecordResult(IngestionRecordResult result)
    {
        lock (_gate)
        {
            switch (result)
            {
                case IngestionRecordResult.Recorded:
                    _processed++;
                    break;
                case IngestionRecordResult.Duplicate:
                    _deduplicated++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown ingestion record result.");
            }
        }
    }

    /// <summary>
    /// Records a message that exhausted its receive endpoint's retry policy, reported by the fault
    /// consumers MassTransit's default fault-publishing behaviour delivers to.
    /// </summary>
    public void RecordDeadLettered()
    {
        lock (_gate)
        {
            _deadLettered++;
        }
    }

    public ProcessingStatsSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new ProcessingStatsSnapshot(_processed, _deduplicated, _deadLettered);
        }
    }
}
