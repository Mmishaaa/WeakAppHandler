namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>What the caller has to do as a result of one evaluation.</summary>
public enum RuleDecisionKind
{
    /// <summary>Nothing to raise or resolve; only the evaluation state changes. See <see cref="RuleDecisionReason"/> for why.</summary>
    None,

    /// <summary>Raise a new active alert and publish AlertRaised.</summary>
    Raise,

    /// <summary>Resolve the open alert with this observation's value and publish AlertResolved.</summary>
    Resolve,
}
