using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public sealed class ReadingRepository(ProcessorDbContext dbContext) : IReadingRepository
{
    public async Task AddRangeAsync(IEnumerable<DbReading> readings, CancellationToken cancellationToken) =>
        await dbContext.Readings.AddRangeAsync(readings, cancellationToken);

    public async Task<IReadOnlyList<DbReading>> GetByMeterAsync(
        Guid meterId,
        string? metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await Filter(meterId, metricCode, from, to)
            .Include(reading => reading.Meter)
            .OrderByDescending(reading => reading.ObservedAt)
            .ThenByDescending(reading => reading.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByMeterAsync(
        Guid meterId,
        string? metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        await Filter(meterId, metricCode, from, to).CountAsync(cancellationToken);

    private IQueryable<DbReading> Filter(
        Guid meterId,
        string? metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var query = dbContext.Readings
            .AsNoTracking()
            .Where(reading => reading.MeterId == meterId);

        if (metricCode is { Length: > 0 })
        {
            query = query.Where(reading => reading.MetricCode == metricCode);
        }

        if (from is { } fromValue)
        {
            query = query.Where(reading => reading.ObservedAt >= fromValue);
        }

        if (to is { } toValue)
        {
            query = query.Where(reading => reading.ObservedAt < toValue);
        }

        return query;
    }
}
