namespace WeakAppHandler.Processor.Worker.Admin;

/// <summary>
/// What <c>GET /api/v1/processing/stats</c> reports (TASK-021): counters since this process started.
/// </summary>
/// <param name="Processed">Messages recorded for the first time.</param>
/// <param name="Deduplicated">Redeliveries of an already-processed message.</param>
/// <param name="DeadLettered">Messages that exhausted retries and moved to a <c>_error</c> queue.</param>
public sealed record ProcessingStatsResponse(int Processed, int Deduplicated, int DeadLettered);
