using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Domain;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Ingestion;

/// <summary>
/// Auto-registers meters, flattens each poll's payloads into <c>readings</c> rows, and compares each
/// value against <c>meter_current_state</c> to detect change and build the <see cref="ReadingStored"/>
/// events the caller publishes (PRD §6 F3). Runs inside <see cref="IngestionRecorder"/>'s open
/// transaction: a meter it creates and the readings/current-state rows it writes for that meter are
/// committed together with the batch row or not at all.
/// </summary>
/// <remarks>
/// The events this returns are not published here. Publishing before the surrounding transaction
/// commits would announce a reading that a later rollback then erases, so
/// <see cref="IngestionRecorder"/> only publishes them once the commit has actually happened.
/// </remarks>
public sealed partial class MeterReadingBatchWriter(
    CoreDbContext dbContext,
    ILogger<MeterReadingBatchWriter> logger) : IReadingBatchWriter
{
    private readonly Dictionary<(string Location, string MeterType), Guid> _meterIds = [];
    private readonly Dictionary<(Guid MeterId, string MetricCode), MeterCurrentState> _currentStates = [];

    public async Task<IReadOnlyList<ReadingStored>> WriteAsync(
        Guid batchId,
        DateTimeOffset observedAt,
        IReadOnlyList<MeterReadingEnvelope> readings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var events = new List<ReadingStored>();

        foreach (var envelope in readings)
        {
            var meterId = await ResolveMeterIdAsync(envelope, observedAt, cancellationToken).ConfigureAwait(false);

            foreach (var value in PayloadNormalizer.Normalize(envelope.Payload))
            {
                var (isChanged, previousValue) = await ApplyCurrentStateAsync(
                    meterId, value, observedAt, cancellationToken).ConfigureAwait(false);

                dbContext.Readings.Add(new Reading
                {
                    MeterId = meterId,
                    MetricCode = value.MetricCode,
                    ObservedAt = observedAt,
                    ValueNumeric = value.ValueNumeric,
                    ValueBool = value.ValueBool,
                    IsChanged = isChanged,
                    BatchId = batchId,
                });

                events.Add(new ReadingStored(
                    meterId,
                    envelope.Location,
                    envelope.MeterType,
                    value.MetricCode,
                    ToMetricValue(value.ValueNumeric, value.ValueBool),
                    previousValue,
                    isChanged,
                    observedAt));
            }
        }

        return events;
    }

    private static MetricValue ToMetricValue(decimal? numeric, bool? boolean) => new((double?)numeric, boolean);

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

    /// <summary>
    /// Compares <paramref name="value"/> against the (meter, metric) pair's current state, upserts
    /// that state, and reports whether the value changed plus what it changed from. A pair with no
    /// prior state is always reported as changed with no previous value — there is nothing to
    /// compare the very first observation of a meter's metric against.
    /// </summary>
    private async Task<(bool IsChanged, MetricValue? PreviousValue)> ApplyCurrentStateAsync(
        Guid meterId,
        NormalizedMetricValue value,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        var key = (meterId, value.MetricCode);

        // Cached rather than re-queried on a repeat within this same batch, for the same reason the
        // meter cache above exists: a row created earlier in this loop has not been saved yet.
        if (!_currentStates.TryGetValue(key, out var state))
        {
            state = await dbContext.MeterCurrentStates
                .FirstOrDefaultAsync(
                    s => s.MeterId == meterId && s.MetricCode == value.MetricCode,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (state is null)
        {
            state = new MeterCurrentState
            {
                MeterId = meterId,
                MetricCode = value.MetricCode,
                ValueNumeric = value.ValueNumeric,
                ValueBool = value.ValueBool,
                ObservedAt = observedAt,
                ChangedAt = observedAt,
            };

            dbContext.MeterCurrentStates.Add(state);
            _currentStates[key] = state;

            return (IsChanged: true, PreviousValue: null);
        }

        var previousValue = ToMetricValue(state.ValueNumeric, state.ValueBool);
        var isChanged = state.ValueNumeric != value.ValueNumeric || state.ValueBool != value.ValueBool;

        state.PreviousValueNumeric = state.ValueNumeric;
        state.PreviousValueBool = state.ValueBool;
        state.ValueNumeric = value.ValueNumeric;
        state.ValueBool = value.ValueBool;

        // observed_at advances on every poll that reports this metric, changed or not; changed_at
        // only moves when the value itself actually moved.
        state.ObservedAt = observedAt;

        if (isChanged)
        {
            state.ChangedAt = observedAt;
        }

        _currentStates[key] = state;

        return (isChanged, previousValue);
    }
}
