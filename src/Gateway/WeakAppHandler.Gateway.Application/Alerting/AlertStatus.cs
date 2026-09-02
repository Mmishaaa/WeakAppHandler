namespace WeakAppHandler.Gateway.Application.Alerting;

/// <summary>
/// Mirrors Notification's <c>AlertStatus</c> (PRD §7.1 `alerts.status`) - a separate type, not a
/// reference to Notification.Api.Domain, for the same reason <see cref="Readings.MeterReadModel"/>
/// does not reference Processor.Domain.Meter: the Gateway takes no compile-time dependency on the
/// service that owns the schema it reads.
/// </summary>
public enum AlertStatus
{
    Active,
    Resolved,
}
