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

    public required Guid BatchId { get; init; }
}
