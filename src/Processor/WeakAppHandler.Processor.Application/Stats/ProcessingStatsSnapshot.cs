namespace WeakAppHandler.Processor.Application.Stats;

/// <summary>
/// A consistent view of <see cref="ProcessingStatsState"/> at one instant, for
/// <c>GET /api/v1/processing/stats</c> (TASK-021).
/// </summary>
/// <param name="Processed">Messages recorded for the first time since this process started.</param>
/// <param name="Deduplicated">Redeliveries of an already-processed message, discarded by <c>processed_messages</c>.</param>
/// <param name="DeadLettered">Messages that exhausted their receive endpoint's retry policy and moved to a <c>_error</c> queue.</param>
public readonly record struct ProcessingStatsSnapshot(int Processed, int Deduplicated, int DeadLettered);
