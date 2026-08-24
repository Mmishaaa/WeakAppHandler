namespace WeakAppHandler.Contracts;

// Published by the Ingestor only for a successful, well-formed WeakApp response. Shares BatchId
// with the IngestAttemptRecorded message from the same poll attempt.
public sealed record ReadingsIngested(
    Guid MessageId,
    Guid BatchId,
    DateTimeOffset FetchedAt,
    int SourceLatencyMs,
    IReadOnlyList<MeterReadingEnvelope> Readings);
