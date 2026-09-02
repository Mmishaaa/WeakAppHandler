namespace WeakAppHandler.Gateway.Application.Alerting;

/// <summary>Mirrors Notification's <c>AlertSeverity</c> (PRD §7.1 `alert_rules.severity`/`alerts.severity`).</summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
}
