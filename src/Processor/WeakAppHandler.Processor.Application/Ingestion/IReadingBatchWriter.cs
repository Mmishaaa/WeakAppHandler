using WeakAppHandler.Contracts;

namespace WeakAppHandler.Processor.Application.Ingestion;

/// <summary>
/// Turns the opaque meter payloads of one poll into <c>readings</c> rows. Called from inside the
/// transaction that writes the <c>ingest_batches</c> row for the same batch, so the batch and the
/// readings it accounts for are committed together or not at all (PRD §6 F3).
/// </summary>
/// <remarks>
/// The seam exists because normalisation itself — meter auto-registration, payload flattening into
/// one row per metric, change detection against <c>meter_current_state</c> — is F3's own subject
/// and is built in TASK-019/TASK-020. Until then <c>NoOpReadingBatchWriter</c> stands in, so the
/// batch bookkeeping and the idempotency ledger this task owns can be finished, exercised and
/// tested without pre-empting how readings get shaped.
/// </remarks>
public interface IReadingBatchWriter
{
    /// <summary>
    /// Persists the readings carried by one batch and returns how many <c>readings</c> rows were
    /// written. The implementation must use the same <c>DbContext</c> as its caller: it is running
    /// inside an open transaction and must not commit or dispose it.
    /// </summary>
    /// <param name="batchId">The <c>ingest_batches</c> row these readings belong to. The row is
    /// already inserted and visible to the current transaction when this is called.</param>
    /// <param name="observedAt">The instant the Ingestor received the response. The source carries
    /// no timestamp of its own, so this is the observation time for every reading in the batch.</param>
    /// <param name="readings">One envelope per meter in the poll, payload still opaque JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> WriteAsync(
        Guid batchId,
        DateTimeOffset observedAt,
        IReadOnlyList<MeterReadingEnvelope> readings,
        CancellationToken cancellationToken);
}
