namespace WeakAppHandler.Processor.Domain;

public sealed class IngestBatch
{
    public required Guid Id { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required IngestBatchOutcome Outcome { get; init; }

    public int? HttpStatus { get; init; }

    public required int DurationMs { get; init; }

    public required int ReadingCount { get; init; }

    public string? ErrorMessage { get; init; }
}
