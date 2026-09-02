namespace WeakAppHandler.Gateway.Application.Alerting;

/// <summary>
/// An alert rule as the read model exposes it (PRD F4/§7.1 `alert_rules`), including the seed rule
/// set TASK-027 applies. <see cref="Location"/>/<see cref="MeterType"/> are nullable because null
/// means "any", matching the rule's own matching semantics rather than an absent filter.
/// </summary>
/// <remarks>
/// Init-only properties rather than a positional constructor, for the same reason as
/// <see cref="AlertReadModel"/>.
/// </remarks>
public sealed record AlertRuleReadModel
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Location { get; init; }

    public string? MeterType { get; init; }

    public required string MetricCode { get; init; }

    public required AlertOperator Operator { get; init; }

    public decimal? ThresholdNumeric { get; init; }

    public bool? ThresholdBool { get; init; }

    public required AlertSeverity Severity { get; init; }

    public required decimal HysteresisPercent { get; init; }

    public required int CooldownSeconds { get; init; }

    public required bool IsEnabled { get; init; }

    public DateTimeOffset? LastTriggeredAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
