using WeakAppHandler.Contracts;

namespace WeakAppHandler.Processor.Application.Ingestion;

/// <summary>
/// Turns the opaque meter payloads of one poll into <c>readings</c> rows. Called from inside the
/// transaction that writes the <c>ingest_batches</c> row for the same batch, so the batch and the
/// readings it accounts for are committed together or not at all (PRD §6 F3).
/// </summary>
/// <remarks>
/// The seam exists because normalisation itself — meter auto-registration, payload flattening into
/// one row per metric, change detection against <c>meter_current_state</c> — is F3's own subject.
/// <c>MeterReadingBatchWriter</c> implements all of it: meter registration and payload flattening
/// (TASK-019), and comparing each value against <c>meter_current_state</c> to produce the
/// <c>ReadingStored</c> events this method returns (TASK-020). The events are not published here —
/// the caller (<c>IngestionRecorder</c>) only publishes them once its surrounding transaction has
/// actually committed.
/// </remarks>
public interface IReadingBatchWriter
{
    /// <summary>
    /// Persists the readings carried by one batch and returns one <see cref="ReadingStored"/> per
    /// <c>readings</c> row written. The implementation must use the same <c>DbContext</c> as its
    /// caller: it is running inside an open transaction and must not commit or dispose it.
    /// </summary>
    /// <param name="batchId">The <c>ingest_batches</c> row these readings belong to. The row is
    /// already inserted and visible to the current transaction when this is called.</param>
    /// <param name="observedAt">The instant the Ingestor received the response. The source carries
    /// no timestamp of its own, so this is the observation time for every reading in the batch.</param>
    /// <param name="readings">One envelope per meter in the poll, payload still opaque JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<ReadingStored>> WriteAsync(
        Guid batchId,
        DateTimeOffset observedAt,
        IReadOnlyList<MeterReadingEnvelope> readings,
        CancellationToken cancellationToken);
}
