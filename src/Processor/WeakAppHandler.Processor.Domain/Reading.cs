namespace WeakAppHandler.Processor.Domain;

public sealed class Reading
{
    public long Id { get; init; }

    public required Guid MeterId { get; init; }

    public required string MetricCode { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    public decimal? ValueNumeric { get; init; }

    public bool? ValueBool { get; init; }

    public required bool IsChanged { get; init; }

    // References ingest_batches.id (added by the pipeline schema in TASK-014); no FK constraint
    // is declared here since that table does not exist yet at this migration.
    public required Guid BatchId { get; init; }
}
