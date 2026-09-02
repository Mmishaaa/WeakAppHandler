using MassTransit;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Stats;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Consumes the outcome of every poll attempt from <c>readings.attempt</c>, successful or not. The
/// Ingestor has no database access, so this message is the only way a failed attempt ever reaches
/// <c>ingest_batches</c> (see tasks.json <c>architecture_review</c>).
/// </summary>
public sealed class IngestAttemptRecordedConsumer(IngestionRecorder recorder, ProcessingStatsState stats)
    : IConsumer<IngestAttemptRecorded>
{
    public async Task Consume(ConsumeContext<IngestAttemptRecorded> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await recorder.RecordAttemptAsync(context.Message, context.CancellationToken)
            .ConfigureAwait(false);

        stats.RecordResult(result);
    }
}
