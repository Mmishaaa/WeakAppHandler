namespace WeakAppHandler.Ingestor.Admin;

/// <summary>
/// Body of <c>PUT /api/v1/ingestion/config</c>. Only the polling interval is adjustable at runtime:
/// the timeouts and circuit-breaker thresholds around a single call are what
/// <c>WeakAppOptionsValidator</c> checks against each other at startup, and letting an admin request
/// move one of them independently would let the pipeline's total budget outgrow the interval.
/// </summary>
/// <param name="PollingIntervalSeconds">
/// New interval. Must be greater than the pipeline's total timeout and at most
/// <see cref="Polling.IngestionRuntimeState.MaxPollingIntervalSeconds"/>.
/// </param>
public sealed record IngestionConfigRequest(int PollingIntervalSeconds);
