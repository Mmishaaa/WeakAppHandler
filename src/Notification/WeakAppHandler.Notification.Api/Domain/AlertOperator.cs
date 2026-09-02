namespace WeakAppHandler.Notification.Api.Domain;

/// <summary>
/// Comparison applied between a reading's value and a rule's threshold (PRD §7.1 `alert_rules.operator`).
/// Persisted as the lower-case wire codes the PRD documents, not as CLR names - see
/// <see cref="Persistence.Converters.AlertOperatorConverter"/>.
/// </summary>
public enum AlertOperator
{
    Gt,
    Gte,
    Lt,
    Lte,
    Eq,
}
