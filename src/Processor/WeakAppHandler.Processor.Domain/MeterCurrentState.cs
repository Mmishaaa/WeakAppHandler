namespace WeakAppHandler.Processor.Domain;

public sealed class MeterCurrentState
{
    public required Guid MeterId { get; init; }

    public required string MetricCode { get; init; }

    public decimal? ValueNumeric { get; set; }

    public bool? ValueBool { get; set; }

    public decimal? PreviousValueNumeric { get; set; }

    public bool? PreviousValueBool { get; set; }

    public required DateTimeOffset ObservedAt { get; set; }

    public required DateTimeOffset ChangedAt { get; set; }
}
