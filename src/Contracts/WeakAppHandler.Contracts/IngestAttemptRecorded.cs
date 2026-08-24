namespace WeakAppHandler.Contracts;

// Published by the Ingestor for every poll attempt, successful or not. The Ingestor has no
// database access, so this is how the Processor learns the outcome and records ingest_batches.
// Shares BatchId with the ReadingsIngested message from the same attempt, when it succeeds.
public sealed record IngestAttemptRecorded(
    Guid MessageId,
    Guid BatchId,
    DateTimeOffset FetchedAt,
    IngestOutcome Outcome,
    int? HttpStatus,
    int DurationMs,
    int ReadingCount,
    string? ErrorMessage);
