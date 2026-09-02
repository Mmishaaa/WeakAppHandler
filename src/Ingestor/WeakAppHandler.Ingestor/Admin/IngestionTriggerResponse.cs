namespace WeakAppHandler.Ingestor.Admin;

/// <summary>
/// What <c>POST /api/v1/ingestion/trigger</c> returns: the outcome of the poll it just ran. The
/// caller gets it directly rather than having to wait for the attempt to come back around through
/// the broker — the same reason <see cref="Polling.IIngestionPoller.PollOnceAsync"/> returns the
/// record it published.
/// </summary>
/// <param name="BatchId">Batch id the attempt published under, so the caller can find the resulting <c>ingest_batches</c> row.</param>
/// <param name="Outcome">Name of the resulting <see cref="Contracts.IngestOutcome"/>.</param>
/// <param name="ReadingCount">Readings this attempt returned; zero for every failed outcome.</param>
/// <param name="HttpStatus">HTTP status of the attempt, null when the call never got a response.</param>
/// <param name="DurationMs">How long the attempt took, including retries.</param>
/// <param name="ErrorMessage">Truncated error, null on success.</param>
/// <param name="FetchedAt">When the response was received; the observation time of every reading in the batch.</param>
public sealed record IngestionTriggerResponse(
    Guid BatchId,
    string Outcome,
    int ReadingCount,
    int? HttpStatus,
    int DurationMs,
    string? ErrorMessage,
    DateTimeOffset FetchedAt);
