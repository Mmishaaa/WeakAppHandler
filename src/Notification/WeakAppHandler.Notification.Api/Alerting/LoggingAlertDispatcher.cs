using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Notification.Api.Alerting;

/// <summary>
/// The default dispatcher until the SignalR hub arrives (TASK-031): it writes the event to the log
/// and nothing else.
/// </summary>
/// <remarks>
/// A no-op would have been enough to satisfy the interface, but then the one thing an operator can
/// check today - that an alert really was raised and later resolved - would exist only in the
/// database. Registered with TryAdd so TASK-031 can replace it without touching this file.
/// </remarks>
public sealed partial class LoggingAlertDispatcher(ILogger<LoggingAlertDispatcher> logger) : IAlertDispatcher
{
    public Task DispatchRaisedAsync(AlertRaised alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        LogRaised(logger, alert.Severity, alert.MetricCode, alert.Location, alert.AlertId);

        return Task.CompletedTask;
    }

    public Task DispatchResolvedAsync(AlertResolved alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        LogResolved(logger, alert.MetricCode, alert.Location, alert.AlertId);

        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Alert raised ({Severity}) for {MetricCode} at {Location}: {AlertId}")]
    private static partial void LogRaised(
        ILogger logger,
        string severity,
        string metricCode,
        string location,
        Guid alertId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Alert resolved for {MetricCode} at {Location}: {AlertId}")]
    private static partial void LogResolved(ILogger logger, string metricCode, string location, Guid alertId);
}
