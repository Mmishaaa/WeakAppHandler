namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>
/// One observed metric value to evaluate rules against - the pure form of a ReadingStored event.
/// </summary>
/// <remarks>
/// A metric is either numeric or boolean (`metrics.value_kind`), so exactly one of
/// <see cref="Numeric"/> and <see cref="Boolean"/> carries the value; a rule of the other kind is
/// reported as <see cref="RuleDecisionReason.NotApplicable"/> rather than coerced.
/// <see cref="ObservedAt"/> is also the instant the engine treats as "now" when it measures cooldown:
/// the engine reads no clock of its own, which is what makes every decision here reproducible from
/// its inputs alone.
/// </remarks>
public sealed record MetricObservation
{
    public required Guid MeterId { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required string MetricCode { get; init; }

    public decimal? Numeric { get; init; }

    public bool? Boolean { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }
}
