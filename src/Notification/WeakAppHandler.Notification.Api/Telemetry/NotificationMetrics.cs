using System.Diagnostics.Metrics;

namespace WeakAppHandler.Notification.Api.Telemetry;

/// <summary>
/// The Notification service's domain metrics (TASK-044, PRD §6 F10): alerts raised and resolved,
/// tagged by severity so a dashboard can distinguish a burst of <c>warning</c> alerts from a single
/// <c>critical</c> one.
/// </summary>
public sealed class NotificationMetrics : IDisposable
{
    public const string MeterName = "WeakAppHandler.Notification";

    private readonly Meter _meter;
    private readonly Counter<long> _alertsRaised;
    private readonly Counter<long> _alertsResolved;

    public NotificationMetrics()
    {
        _meter = new Meter(MeterName);
        _alertsRaised = _meter.CreateCounter<long>(
            "notification.alerts.raised",
            unit: "{alert}",
            description: "Alerts transitioned from inactive to active, tagged by severity.");
        _alertsResolved = _meter.CreateCounter<long>(
            "notification.alerts.resolved",
            unit: "{alert}",
            description: "Alerts transitioned from active to resolved, tagged by severity.");
    }

    /// <summary>
    /// Exposed so tests can attach a <see cref="MeterListener"/> scoped to this exact instance rather
    /// than by meter name - multiple <see cref="NotificationMetrics"/> instances share the same name
    /// across parallel test hosts, and a listener filtering by name alone would pick up another
    /// test's measurements too.
    /// </summary>
    public Meter Meter => _meter;

    public void RecordRaised(string severity) =>
        _alertsRaised.Add(1, new KeyValuePair<string, object?>("severity", severity));

    public void RecordResolved(string severity) =>
        _alertsResolved.Add(1, new KeyValuePair<string, object?>("severity", severity));

    public void Dispose() => _meter.Dispose();
}
