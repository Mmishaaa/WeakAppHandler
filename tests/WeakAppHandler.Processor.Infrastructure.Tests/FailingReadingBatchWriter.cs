using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// Fails halfway through writing a batch: it stores one reading row and then throws, which is the
/// only honest way to test that the <c>ingest_batches</c> row, the readings and the idempotency
/// ledger entry are one transaction rather than three separate writes that merely usually succeed
/// together.
/// </summary>
internal sealed class FailingReadingBatchWriter(CoreDbContext dbContext, Guid meterId) : IReadingBatchWriter
{
    public const string FailureMessage = "Normalisation failed halfway through the batch.";

    public async Task<IReadOnlyList<ReadingStored>> WriteAsync(
        Guid batchId,
        DateTimeOffset observedAt,
        IReadOnlyList<MeterReadingEnvelope> readings,
        CancellationToken cancellationToken)
    {
        dbContext.Readings.Add(new Reading
        {
            MeterId = meterId,
            MetricCode = TestReadingBatchWriter.MetricCode,
            ObservedAt = observedAt,
            ValueNumeric = 1m,
            IsChanged = true,
            BatchId = batchId,
        });

        // Saved, not merely tracked: the row is really in the database when the failure happens, so
        // only the surrounding transaction can take it back out again.
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        throw new InvalidOperationException(FailureMessage);
    }
}
