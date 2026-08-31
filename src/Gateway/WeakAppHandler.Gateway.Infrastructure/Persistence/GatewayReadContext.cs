using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Gateway.Application.Readings;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence;

public sealed class GatewayReadContext(GatewayReadDbContext dbContext) : IGatewayReadContext
{
    public IQueryable<MeterReadModel> Meters => dbContext.Meters
        .Select(m => new MeterReadModel
        {
            Id = m.Id,
            Location = m.Location,
            MeterType = m.MeterType,
            FirstSeenAt = m.FirstSeenAt,
            LastSeenAt = m.LastSeenAt,
        });

    // The owning meter's location/meterType are joined in here rather than exposed as a nested
    // GraphQL field, so `readings(where: { location: ... })` becomes one SQL WHERE on a joined
    // column instead of a second round trip per row.
    public IQueryable<ReadingReadModel> Readings => dbContext.Readings.Join(
        dbContext.Meters,
        reading => reading.MeterId,
        meter => meter.Id,
        (reading, meter) => new ReadingReadModel
        {
            Id = reading.Id,
            MeterId = reading.MeterId,
            Location = meter.Location,
            MeterType = meter.MeterType,
            MetricCode = reading.MetricCode,
            ObservedAt = reading.ObservedAt,
            ValueNumeric = reading.ValueNumeric,
            ValueBool = reading.ValueBool,
            IsChanged = reading.IsChanged,
        });

    public async Task<IReadOnlyList<MeterCurrentValueReadModel>> GetCurrentValuesAsync(
        IReadOnlyList<Guid> meterIds,
        CancellationToken cancellationToken) =>
        await dbContext.MeterCurrentStates
            .Where(s => meterIds.Contains(s.MeterId))
            .Select(s => new MeterCurrentValueReadModel(
                s.MeterId,
                s.MetricCode,
                s.ValueNumeric,
                s.ValueBool,
                s.PreviousValueNumeric,
                s.PreviousValueBool,
                s.ObservedAt,
                s.ChangedAt))
            .ToListAsync(cancellationToken);
}
