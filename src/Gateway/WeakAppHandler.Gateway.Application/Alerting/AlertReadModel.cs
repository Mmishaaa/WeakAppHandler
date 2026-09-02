namespace WeakAppHandler.Gateway.Application.Alerting;

/// <summary>
/// An alert as the read model exposes it (PRD F4/§7.1 `alerts`), denormalised location/meterType/
/// metricCode included exactly as Notification stores them - the Gateway never joins into the core
/// schema to serve this feed, matching how Notification never joins into it to raise the alert either.
/// </summary>
/// <remarks>
/// Init-only properties rather than a positional constructor: <c>[UseProjection]</c> needs a
/// parameterless constructor, the same reason <see cref="Readings.MeterReadModel"/> is shaped this way.
/// </remarks>
public sealed record AlertReadModel
{
    public required Guid Id { get; init; }

    public required Guid RuleId { get; init; }

    public required Guid MeterId { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required string MetricCode { get; init; }

    public required AlertStatus Status { get; init; }

    public required AlertSeverity Severity { get; init; }

    public required DateTimeOffset TriggeredAt { get; init; }

    public decimal? TriggeredValueNumeric { get; init; }

    public bool? TriggeredValueBool { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public decimal? ResolvedValueNumeric { get; init; }

    public bool? ResolvedValueBool { get; init; }
}
