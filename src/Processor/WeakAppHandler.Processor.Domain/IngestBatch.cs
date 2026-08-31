namespace WeakAppHandler.Processor.Domain;

/// <summary>
/// The record of one poll attempt against WeakApp, successful or not (PRD §7.1). One row per
/// batch id, written from the two messages the Ingestor publishes for that attempt.
/// </summary>
/// <remarks>
/// The outcome-carrying fields are mutable because the row can be created by either message: the
/// readings of a successful poll are published before the attempt record and normally arrive first,
/// so the readings consumer inserts a provisional row and the attempt record — the authoritative
/// account of what happened — overwrites it when it lands.
/// </remarks>
public sealed class IngestBatch
{
    public required Guid Id { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required IngestBatchOutcome Outcome { get; set; }

    public int? HttpStatus { get; set; }

    public required int DurationMs { get; set; }

    /// <summary>
    /// How many meter readings the poll returned, as reported by the Ingestor. Zero for failures.
    /// </summary>
    public required int ReadingCount { get; set; }

    public string? ErrorMessage { get; set; }
}
