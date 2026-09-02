namespace WeakAppHandler.Notification.Api.Domain;

/// <summary>
/// Lifecycle of an alert (PRD §7.1 `alerts.status`). There is no third "acknowledged" state: §6.6
/// defines resolution as the only transition out of <see cref="Active"/>.
/// </summary>
public enum AlertStatus
{
    Active,
    Resolved,
}
