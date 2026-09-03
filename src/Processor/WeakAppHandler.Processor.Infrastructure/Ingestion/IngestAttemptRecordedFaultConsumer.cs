using MassTransit;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Stats;
using WeakAppHandler.Processor.Application.Telemetry;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Consumes the <see cref="Fault{T}"/> MassTransit publishes once an <see cref="IngestAttemptRecorded"/>
/// delivery exhausts its receive endpoint's retry policy and moves to
/// <c>readings.attempt_error</c> (TASK-021). See <see cref="ReadingsIngestedFaultConsumer"/> for why
/// this is a second, independently-bound consumer rather than folded into the one it shadows.
/// </summary>
public sealed class IngestAttemptRecordedFaultConsumer(ProcessingStatsState stats, ProcessorMetrics metrics)
    : IConsumer<Fault<IngestAttemptRecorded>>
{
    public Task Consume(ConsumeContext<Fault<IngestAttemptRecorded>> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        stats.RecordDeadLettered();
        metrics.RecordDeadLettered();

        return Task.CompletedTask;
    }
}
