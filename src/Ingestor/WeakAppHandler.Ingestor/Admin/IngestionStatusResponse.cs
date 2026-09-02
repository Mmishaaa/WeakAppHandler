namespace WeakAppHandler.Ingestor.Admin;

/// <summary>
/// What <c>GET /api/v1/ingestion/status</c> reports (TASK-017): the last poll's outcome, failure
/// counts per reason, the circuit breaker's state and the interval currently in force. Every
/// "last..." field is null before the first poll completes.
/// </summary>
/// <param name="LastOutcome">Name of the last <see cref="Contracts.IngestOutcome"/>, e.g. <c>Success</c> or <c>HttpError</c>.</param>
/// <param name="LastPolledAt">When the last attempt's response was received.</param>
/// <param name="LastSuccessAt">When the last <em>successful</em> attempt's response was received — how stale the data is.</param>
/// <param name="LastBatchId">Batch id of the last attempt, which ties this to the Processor's <c>ingest_batches</c> row.</param>
/// <param name="LastReadingCount">Readings the last attempt returned; zero for every failed outcome.</param>
/// <param name="LastHttpStatus">HTTP status of the last attempt, null when the call never got a response.</param>
/// <param name="LastDurationMs">How long the last attempt took, including retries.</param>
/// <param name="LastErrorMessage">Truncated error of the last attempt, null on success.</param>
/// <param name="TotalPolls">Attempts completed since this process started.</param>
/// <param name="FailureCountsByReason">Failed attempts per outcome name; successes are counted by <paramref name="TotalPolls"/> alone.</param>
/// <param name="CircuitBreakerState">Polly's <c>CircuitState</c>: <c>Closed</c>, <c>Open</c>, <c>HalfOpen</c> or <c>Isolated</c>.</param>
/// <param name="PollingIntervalSeconds">The interval the loop is scheduling on right now, which <c>PUT config</c> can change.</param>
public sealed record IngestionStatusResponse(
    string? LastOutcome,
    DateTimeOffset? LastPolledAt,
    DateTimeOffset? LastSuccessAt,
    Guid? LastBatchId,
    int? LastReadingCount,
    int? LastHttpStatus,
    int? LastDurationMs,
    string? LastErrorMessage,
    int TotalPolls,
    IReadOnlyDictionary<string, int> FailureCountsByReason,
    string CircuitBreakerState,
    int PollingIntervalSeconds);
