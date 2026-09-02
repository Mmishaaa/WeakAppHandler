namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>
/// What the engine remembers between two observations for one (rule, meter, metric) - the pure form
/// of an `alert_rule_state` row plus the "is there an alert open right now" fact.
/// </summary>
/// <remarks>
/// <see cref="WasBreaching"/> comes from persisted state rather than from the previous value carried
/// on the event, so transition detection survives a restart: after a restart the previous value is
/// whatever the next poll happens to report, while the stored flag still knows the window was already
/// open and therefore that no second alert is due.
/// </remarks>
public sealed record RuleEvaluationState
{
    /// <summary>State for a (rule, meter, metric) pairing the engine has never seen before.</summary>
    public static RuleEvaluationState Initial { get; } = new() { WasBreaching = false, HasActiveAlert = false };

    /// <summary>Whether the previous evaluation of this pairing found the rule breaching.</summary>
    public required bool WasBreaching { get; init; }

    /// <summary>Whether an alert raised by this rule for this meter and metric is still `active`.</summary>
    public required bool HasActiveAlert { get; init; }

    /// <summary>When this pairing last raised an alert; null if it never has. Cooldown is measured from here.</summary>
    public DateTimeOffset? LastTriggeredAt { get; init; }
}
