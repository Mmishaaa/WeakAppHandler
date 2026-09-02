namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>
/// Why an evaluation came out the way it did. Present because "no alert" has several very different
/// causes and telling them apart is the difference between a working suppression policy and a lost
/// alert - the consumer logs this, and the unit tests assert on it instead of only on the absence of
/// an alert, which every branch would satisfy equally.
/// </summary>
public enum RuleDecisionReason
{
    /// <summary>The value crossed into breach and nothing suppressed it: an alert is due.</summary>
    Raised,

    /// <summary>The value retreated past the hysteresis band while an alert was open: the alert is over.</summary>
    Resolved,

    /// <summary>
    /// The rule cannot be evaluated against this observation - a numeric rule against a boolean
    /// metric or the reverse, a rule with both or neither threshold set, or a non-`eq` operator on a
    /// boolean rule. The state is left untouched rather than guessed at.
    /// </summary>
    NotApplicable,

    /// <summary>Breaching, but it already was on the previous observation: transition-triggered alerting fires once (PRD §6.6).</summary>
    NoTransition,

    /// <summary>Breaching and a transition, but the alert raised earlier for this pairing is still open.</summary>
    AlertAlreadyActive,

    /// <summary>Breaching and a transition, but this (rule, meter, metric) fired too recently.</summary>
    CooldownActive,

    /// <summary>Not breaching, but the value has not yet retreated past the threshold by the hysteresis margin, so the open alert stands.</summary>
    WithinHysteresisBand,

    /// <summary>Not breaching and no alert open: the normal case.</summary>
    NotBreaching,
}
