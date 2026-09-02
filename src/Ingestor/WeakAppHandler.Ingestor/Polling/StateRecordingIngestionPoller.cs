using WeakAppHandler.Contracts;

namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// Wraps <see cref="IngestionPoller"/> so every completed attempt lands in
/// <see cref="IngestionRuntimeState"/>, whichever path started it — the timer loop or
/// <c>POST /api/v1/ingestion/trigger</c>. A decorator rather than a line inside the poller because
/// the poller's job is to call WeakApp and publish what happened, with a publish ordering that is
/// load-bearing; observing outcomes for the admin API is a separate concern that must simply never
/// be forgotten by a future caller.
/// </summary>
/// <remarks>
/// An attempt that throws is deliberately not recorded. Every outcome WeakApp can produce — including
/// every failure — comes back as an <see cref="IngestAttemptRecorded"/>, so an exception here means
/// the attempt failed outside the resilience pipeline (a broker publish, say) and produced no outcome
/// to report. The loop logs it; inventing a synthetic failure reason for it would put something in
/// the status counters that no <c>ingest_batches</c> row will ever corroborate.
/// </remarks>
internal sealed class StateRecordingIngestionPoller(IngestionPoller inner, IngestionRuntimeState state)
    : IIngestionPoller
{
    public async Task<IngestAttemptRecorded> PollOnceAsync(CancellationToken cancellationToken)
    {
        var attempt = await inner.PollOnceAsync(cancellationToken).ConfigureAwait(false);
        state.RecordAttempt(attempt);

        return attempt;
    }
}
