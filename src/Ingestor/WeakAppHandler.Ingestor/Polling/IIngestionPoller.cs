using WeakAppHandler.Contracts;

namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// Performs exactly one poll of WeakApp and publishes the resulting messages. Kept separate from
/// <see cref="IngestionWorker"/>'s scheduling so a poll can also be started on demand — TASK-017's
/// <c>POST /api/v1/ingestion/trigger</c> is the same operation, just triggered by a request rather
/// than by the timer.
/// </summary>
public interface IIngestionPoller
{
    /// <summary>
    /// Runs one poll attempt and returns the record that was published for it, so a caller that
    /// triggered the poll can report the batch outcome without waiting for a round trip through
    /// the broker.
    /// </summary>
    public Task<IngestAttemptRecorded> PollOnceAsync(CancellationToken cancellationToken);
}
