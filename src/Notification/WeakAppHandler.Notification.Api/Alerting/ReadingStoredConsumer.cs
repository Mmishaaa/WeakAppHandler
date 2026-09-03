using MassTransit;
using WeakAppHandler.Contracts;
using WeakAppHandler.Notification.Api.Telemetry;

namespace WeakAppHandler.Notification.Api.Alerting;

/// <summary>
/// Consumes every stored reading from <c>readings.stored</c> and turns the alerts it produced into
/// dispatched events (PRD §6 F6).
/// </summary>
/// <remarks>
/// Dispatch happens here rather than inside <see cref="AlertEvaluator"/> so that it can only happen
/// after the evaluator's write has committed - the evaluator returns events, it does not announce
/// them.
/// </remarks>
public sealed class ReadingStoredConsumer(AlertEvaluator evaluator, IAlertDispatcher dispatcher, NotificationMetrics metrics)
    : IConsumer<ReadingStored>
{
    public async Task Consume(ConsumeContext<ReadingStored> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await evaluator
            .EvaluateAsync(context.Message, context.CancellationToken)
            .ConfigureAwait(false);

        foreach (var alert in result.Raised)
        {
            await dispatcher.DispatchRaisedAsync(alert, context.CancellationToken).ConfigureAwait(false);
            metrics.RecordRaised(alert.Severity);
        }

        foreach (var alert in result.Resolved)
        {
            await dispatcher.DispatchResolvedAsync(alert, context.CancellationToken).ConfigureAwait(false);
            metrics.RecordResolved(alert.Severity);
        }
    }
}
