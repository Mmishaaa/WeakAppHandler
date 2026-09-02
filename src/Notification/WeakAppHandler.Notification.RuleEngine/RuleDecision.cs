namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>
/// The outcome of evaluating one rule against one observation: what to do, why, and the breach flag
/// to persist for the next evaluation.
/// </summary>
/// <remarks>
/// <see cref="IsBreaching"/> is part of the result rather than something the caller recomputes: it is
/// exactly what belongs in `alert_rule_state.was_breaching`, and a caller that derived it a second
/// time could disagree with the decision it is storing it alongside. For
/// <see cref="RuleDecisionReason.NotApplicable"/> it echoes the previous flag, because an evaluation
/// that could not happen must not be recorded as a transition in either direction.
/// </remarks>
public sealed record RuleDecision
{
    public required RuleDecisionKind Kind { get; init; }

    public required RuleDecisionReason Reason { get; init; }

    /// <summary>The breach flag to store for this (rule, meter, metric) before the next observation.</summary>
    public required bool IsBreaching { get; init; }
}
