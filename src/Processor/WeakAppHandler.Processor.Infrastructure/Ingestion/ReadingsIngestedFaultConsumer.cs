using MassTransit;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Stats;
using WeakAppHandler.Processor.Application.Telemetry;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Consumes the <see cref="Fault{T}"/> MassTransit publishes once a <see cref="ReadingsIngested"/>
/// delivery exhausts its receive endpoint's retry policy and moves to
/// <c>readings.ingested_error</c> (TASK-021). Not bound to an explicit receive endpoint like the
/// consumer it shadows — <c>ConfigureEndpoints</c> gives it its own convention-named queue, since a
/// dead-lettered message and the fault event describing it are two independent deliveries.
/// </summary>
public sealed class ReadingsIngestedFaultConsumer(ProcessingStatsState stats, ProcessorMetrics metrics)
    : IConsumer<Fault<ReadingsIngested>>
{
    public Task Consume(ConsumeContext<Fault<ReadingsIngested>> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        stats.RecordDeadLettered();
        metrics.RecordDeadLettered();

        return Task.CompletedTask;
    }
}
