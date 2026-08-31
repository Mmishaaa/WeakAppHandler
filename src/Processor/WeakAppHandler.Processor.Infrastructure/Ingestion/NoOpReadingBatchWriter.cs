using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Ingestion;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Stands in for payload normalisation until TASK-019/TASK-020 build it: meter auto-registration,
/// flattening a payload into one <c>readings</c> row per metric and change detection against
/// <c>meter_current_state</c> are F3's own subject, and guessing at them here would be work thrown
/// away. It writes nothing and reports zero rows, so an <c>ingest_batches</c> row currently records
/// how many meter readings the poll returned while no <c>readings</c> rows exist for it yet.
/// </summary>
public sealed partial class NoOpReadingBatchWriter(ILogger<NoOpReadingBatchWriter> logger) : IReadingBatchWriter
{
    public Task<int> WriteAsync(
        Guid batchId,
        DateTimeOffset observedAt,
        IReadOnlyList<MeterReadingEnvelope> readings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readings);

        LogNormalisationPending(logger, readings.Count, batchId);

        return Task.FromResult(0);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Payload normalisation is not implemented yet; {MeterCount} meter payloads from batch {BatchId} were not stored as readings")]
    private static partial void LogNormalisationPending(ILogger logger, int meterCount, Guid batchId);
}
