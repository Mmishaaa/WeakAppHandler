using WeakAppHandler.Gateway.Application.Alerting;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

/// <summary>Maps onto the <c>alert_rules</c> table Notification owns and migrates (PRD §7.1).</summary>
public sealed class AlertRuleEntity
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
