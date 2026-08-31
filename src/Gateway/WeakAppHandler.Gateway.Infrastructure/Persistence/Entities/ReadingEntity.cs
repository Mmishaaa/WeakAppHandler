namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

/// <summary>Maps onto the <c>readings</c> table Processor owns and migrates (PRD §7.1).</summary>
public sealed class ReadingEntity
{
    public required long Id { get; init; }

    public required Guid MeterId { get; init; }

    public required string MetricCode { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    public decimal? ValueNumeric { get; init; }

    public bool? ValueBool { get; init; }

    public required bool IsChanged { get; init; }
}
