using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Auto-registers meters and flattens each poll's payloads into <c>readings</c> rows (PRD §6 F3).
/// Runs inside <see cref="IngestionRecorder"/>'s open transaction: a meter it creates and the
/// readings it writes for that meter are committed together with the batch row or not at all.
/// </summary>
/// <remarks>
/// <c>readings.is_changed</c> is written as <see langword="true"/> for every row here. Comparing a
/// new value against <c>meter_current_state</c> and publishing <see cref="ReadingStored"/> is
/// TASK-020's own subject, not this writer's — it only flattens payloads and registers meters.
/// </remarks>
public sealed partial class MeterReadingBatchWriter(
    CoreDbContext dbContext,
    ILogger<MeterReadingBatchWriter> logger) : IReadingBatchWriter
{
    private readonly Dictionary<(string Location, string MeterType), Guid> _meterIds = [];

    public async Task<int> WriteAsync(
        Guid batchId,
        DateTimeOffset observedAt,
        IReadOnlyList<MeterReadingEnvelope> readings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var rowCount = 0;

        foreach (var envelope in readings)
        {
            var meterId = await ResolveMeterIdAsync(envelope, observedAt, cancellationToken).ConfigureAwait(false);

            foreach (var value in PayloadNormalizer.Normalize(envelope.Payload))
            {
                dbContext.Readings.Add(new Reading
                {
                    MeterId = meterId,
                    MetricCode = value.MetricCode,
                    ObservedAt = observedAt,
                    ValueNumeric = value.ValueNumeric,
                    ValueBool = value.ValueBool,
                    IsChanged = true,
                    BatchId = batchId,
                });
                rowCount++;
            }
        }

        return rowCount;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Auto-registered meter {Location}/{MeterType} as {MeterId}")]
    private static partial void LogMeterRegistered(ILogger logger, string location, string meterType, Guid meterId);

    private async Task<Guid> ResolveMeterIdAsync(
        MeterReadingEnvelope envelope,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        var key = (envelope.Location, envelope.MeterType);

        // Cached rather than re-queried on a repeat within this same batch: a meter added earlier in
        // this loop has not been saved yet, so a second query would miss it and insert a duplicate
        // against the (location, meter_type) natural key. Its last_seen_at is already this poll's
        // observedAt from whichever branch resolved it the first time.
        if (_meterIds.TryGetValue(key, out var cachedId))
        {
            return cachedId;
        }

        var existing = await dbContext.Meters
            .FirstOrDefaultAsync(
                m => m.Location == envelope.Location && m.MeterType == envelope.MeterType,
                cancellationToken)
            .ConfigureAwait(false);

        Guid meterId;

        if (existing is null)
        {
            meterId = Guid.NewGuid();

            dbContext.Meters.Add(new Meter
            {
                Id = meterId,
                Location = envelope.Location,
                MeterType = envelope.MeterType,
                FirstSeenAt = observedAt,
                LastSeenAt = observedAt,
            });

            LogMeterRegistered(logger, envelope.Location, envelope.MeterType, meterId);
        }
        else
        {
            meterId = existing.Id;
            existing.LastSeenAt = observedAt;
        }

        _meterIds[key] = meterId;

        return meterId;
    }
}
