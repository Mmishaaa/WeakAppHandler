using WeakAppHandler.Contracts;

namespace WeakAppHandler.Notification.Api.Alerting;

/// <summary>
/// Where a persisted alert goes once it exists (PRD §6.7). Dispatch is in-process on purpose: the
/// only consumer of these events is this same service's SignalR hub (TASK-031), so putting them back
/// on RabbitMQ would mean publishing to an exchange whose single subscriber is the publisher.
/// </summary>
/// <remarks>
/// Implementations are called after the alert has been committed, never before: a subscriber that
/// was told about an alert a later rollback erased has no way to un-see it. They are also called on
/// the consumer's thread, so an implementation that can block or fail slowly should do its own
/// fan-out rather than hold the message up - a dispatch failure faults the message and sends it back
/// through the endpoint's retry policy, which would re-evaluate a reading whose alert already exists.
/// </remarks>
public interface IAlertDispatcher
{
    public Task DispatchRaisedAsync(AlertRaised alert, CancellationToken cancellationToken);

    public Task DispatchResolvedAsync(AlertResolved alert, CancellationToken cancellationToken);
}
