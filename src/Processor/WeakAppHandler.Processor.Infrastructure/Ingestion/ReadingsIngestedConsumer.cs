using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Consumes the readings of one successful poll from <c>readings.ingested</c>. A redelivery is
/// acknowledged and discarded rather than faulted, which is what keeps at-least-once delivery from
/// producing duplicate rows (PRD §6 F3).
/// </summary>
public sealed class ReadingsIngestedConsumer(IngestionRecorder recorder) : IConsumer<ReadingsIngested>
{
    public async Task Consume(ConsumeContext<ReadingsIngested> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await recorder.RecordReadingsAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
    }
}
