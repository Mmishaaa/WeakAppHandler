namespace WeakAppHandler.Notification.Api.Domain;

/// <summary>
/// Per (rule, meter, metric) evaluation state: what the rule engine has to remember between two
/// ReadingStored events to make triggering transition-based and cooldown local to one meter.
/// </summary>
/// <remarks>
/// This table is the reason cooldown is not read off <see cref="AlertRule.LastTriggeredAt"/>: a
/// single `last_triggered_at` on the rule would let a breach in the Kitchen silence a breach in the
/// Garage for the whole cooldown window, which is a lost alert rather than a suppressed duplicate.
/// <see cref="WasBreaching"/> is persisted rather than derived from the incoming event's
/// previousValue so that transition detection survives a restart of the service.
/// </remarks>
public sealed class AlertRuleState
{
    public required Guid RuleId { get; init; }

    public required Guid MeterId { get; init; }

    public required string MetricCode { get; init; }

    public required bool WasBreaching { get; set; }

    public DateTimeOffset? LastTriggeredAt { get; set; }

    public required DateTimeOffset LastEvaluatedAt { get; set; }
}
