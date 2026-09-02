using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.Notification.Api.Alerting;

namespace WeakAppHandler.Notification.Api.RealTime;

/// <summary>
/// Pushes persisted alert events to every client connected to <see cref="AlertsHub"/> (TASK-031, PRD
/// §6.7). Registered in Program.cs ahead of AlertingServiceCollectionExtensions.AddAlerting's
/// TryAddSingleton, so it — not LoggingAlertDispatcher — is what IAlertDispatcher resolves to.
/// </summary>
public sealed partial class SignalRAlertDispatcher(
    IHubContext<AlertsHub> hub,
    ILogger<SignalRAlertDispatcher> logger) : IAlertDispatcher
{
    private const string AlertRaisedMethod = "AlertRaised";
    private const string AlertResolvedMethod = "AlertResolved";

    public Task DispatchRaisedAsync(AlertRaised alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        return SendAsync(AlertRaisedMethod, alert, alert.AlertId, cancellationToken);
    }

    public Task DispatchResolvedAsync(AlertResolved alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        return SendAsync(AlertResolvedMethod, alert, alert.AlertId, cancellationToken);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to broadcast {Method} for alert {AlertId} over SignalR")]
    private static partial void LogDispatchFailed(ILogger logger, string method, Guid alertId, Exception exception);

    /// <summary>
    /// IAlertDispatcher's own contract requires implementations not to hold up or fault the consumer
    /// on a dispatch failure: an already-committed alert must not be re-evaluated just because a
    /// broadcast to a client that has since disconnected failed. A push nobody was connected to
    /// receive is lost rather than requeued — PRD §6.7 covers this on the client side through
    /// reconnect-then-refetch, not through hub-side redelivery.
    /// </summary>
    private async Task SendAsync(string method, object payload, Guid alertId, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.All.SendAsync(method, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        // A broadcast failure (e.g. a client dropping mid-send) must not fault the RabbitMQ message
        // this dispatch was called from — see the summary above.
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogDispatchFailed(logger, method, alertId, ex);
        }
    }
}
