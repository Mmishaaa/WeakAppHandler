namespace WeakAppHandler.Notification.Api.Domain;

/// <summary>
/// Severity of a rule and of the alerts it raises (PRD §7.1 `alert_rules.severity`, copied onto
/// `alerts.severity` at trigger time so an alert keeps the severity it was raised under even if the
/// rule is later edited).
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
}
