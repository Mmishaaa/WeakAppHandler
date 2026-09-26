using System.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public sealed class ReadingRepository(ProcessorDbContext dbContext) : IReadingRepository
{
    public async Task AddRangeAsync(IEnumerable<DbReading> readings, CancellationToken cancellationToken) =>
        await dbContext.Readings.AddRangeAsync(readings, cancellationToken);

    public async Task<ReadingPageAggregate> GetPageByMeterAsync(
        Guid meterId,
        string? metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        var query = Filter(meterId, metricCode, from, to);

        var totalCount = await query.CountAsync(cancellationToken);

        IReadOnlyList<DbReading> items = skip >= totalCount
            ? []
            : await query
                .OrderByDescending(reading => reading.ObservedAt)
                .ThenByDescending(reading => reading.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ReadingPageAggregate(items, totalCount);
    }

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
