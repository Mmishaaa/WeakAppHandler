using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// Stands in for the normalisation TASK-019 builds, writing exactly one <c>readings</c> row per
/// meter envelope so a test can count rows and see whether the batch and its readings were really
/// committed together. Deliberately minimal — it registers a meter and stores a fixed metric rather
/// than flattening payloads — because what is under test here is the transaction and the ledger,
/// not the shape of a reading.
/// </summary>
internal sealed class TestReadingBatchWriter(CoreDbContext dbContext) : IReadingBatchWriter
{
    /// <summary>One of the metrics seeded by the core migration, so the foreign key holds.</summary>
    public const string MetricCode = "co2";

    private readonly Dictionary<(string Location, string MeterType), Guid> _meterIds = [];

    public int Invocations { get; private set; }

    public async Task<int> WriteAsync(
        Guid batchId,
        DateTimeOffset observedAt,
        IReadOnlyList<MeterReadingEnvelope> readings,
        CancellationToken cancellationToken)
    {
        Invocations++;

        foreach (var envelope in readings)
        {
            var meterId = await ResolveMeterIdAsync(envelope, observedAt, cancellationToken).ConfigureAwait(false);

            dbContext.Readings.Add(new Reading
            {
                MeterId = meterId,
                MetricCode = MetricCode,
                ObservedAt = observedAt,
                ValueNumeric = 1m,
                IsChanged = true,
                BatchId = batchId,
            });
        }

        return readings.Count;
    }

    private async Task<Guid> ResolveMeterIdAsync(
        MeterReadingEnvelope envelope,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        var key = (envelope.Location, envelope.MeterType);

        // Cached rather than re-queried: a meter added earlier in this same batch has not been saved
        // yet, so a second query would miss it and insert a duplicate against the natural key.
        if (_meterIds.TryGetValue(key, out var cachedId))
        {
            return cachedId;
        }

        var existing = await dbContext.Meters
            .FirstOrDefaultAsync(
                m => m.Location == envelope.Location && m.MeterType == envelope.MeterType,
                cancellationToken)
            .ConfigureAwait(false);

        var meterId = existing?.Id ?? Guid.NewGuid();

        if (existing is null)
        {
            dbContext.Meters.Add(new Meter
            {
                Id = meterId,
                Location = envelope.Location,
                MeterType = envelope.MeterType,
                FirstSeenAt = observedAt,
                LastSeenAt = observedAt,
            });
        }

        _meterIds[key] = meterId;

        return meterId;
    }
}
