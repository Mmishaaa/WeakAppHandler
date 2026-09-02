using WeakAppHandler.Gateway.Application.Alerting;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

/// <summary>
/// Maps onto the <c>alerts</c> table Notification owns and migrates (PRD §7.1). The Gateway never
/// creates or alters this table - it only ever selects from it - so this type exists purely to give
/// EF Core something to map columns onto; it carries no domain behaviour.
/// </summary>
public sealed class AlertEntity
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
