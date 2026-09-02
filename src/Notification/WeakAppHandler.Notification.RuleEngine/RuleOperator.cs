namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>
/// Comparison a rule applies between an observed value and its threshold (PRD §7.1
/// `alert_rules.operator`).
/// </summary>
/// <remarks>
/// Deliberately a separate enum from the persistence-side `AlertOperator`, even though the members
/// are identical: this project must stay reference-free (TASK-028), so it cannot see the API's domain
/// types, and the API maps between the two. The upside is that a future persistence-only concern
/// (say an operator that only makes sense for a stored rule) cannot leak into the engine.
/// </remarks>
public enum RuleOperator
{
    /// <summary>Breach when the value is strictly greater than the threshold.</summary>
    Gt,

    /// <summary>Breach when the value is greater than or equal to the threshold.</summary>
    Gte,

    /// <summary>Breach when the value is strictly less than the threshold.</summary>
    Lt,

    /// <summary>Breach when the value is less than or equal to the threshold.</summary>
    Lte,

    /// <summary>Breach when the value equals the threshold. The only operator a boolean rule may use.</summary>
    Eq,
}
