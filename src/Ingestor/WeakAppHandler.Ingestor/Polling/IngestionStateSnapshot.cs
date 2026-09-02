using WeakAppHandler.Contracts;

namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// A point-in-time copy of <see cref="IngestionRuntimeState"/>. <paramref name="LastAttempt"/> is
/// null until the first poll completes, which is a real state the admin API has to report rather
/// than paper over: a service that has just started has no outcome yet.
/// </summary>
public sealed record IngestionStateSnapshot(
    IngestAttemptRecorded? LastAttempt,
    DateTimeOffset? LastSuccessAt,
    int TotalPolls,
    IReadOnlyDictionary<IngestOutcome, int> FailureCountsByOutcome,
    TimeSpan PollingInterval);
