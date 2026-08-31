namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

/// <summary>Maps onto the <c>meter_current_state</c> table Processor owns and migrates (PRD §7.1).</summary>
public sealed class MeterCurrentStateEntity
{
    public required Guid MeterId { get; init; }

    public required string MetricCode { get; init; }

    public decimal? ValueNumeric { get; init; }

    public bool? ValueBool { get; init; }

    public decimal? PreviousValueNumeric { get; init; }

    public bool? PreviousValueBool { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
