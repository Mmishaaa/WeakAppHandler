namespace WeakAppHandler.Gateway.Application.Alerting;

/// <summary>Mirrors Notification's <c>AlertOperator</c> (PRD §7.1 `alert_rules.operator`).</summary>
public enum AlertOperator
{
    Gt,
    Gte,
    Lt,
    Lte,
    Eq,
}
