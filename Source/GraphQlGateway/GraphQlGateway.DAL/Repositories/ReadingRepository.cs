using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace GraphQlGateway.DAL.Repositories;

public sealed class ReadingRepository(IDbContextFactory<GatewayDbContext> dbContextFactory)
    : IReadingRepository
{
    public async Task<IReadOnlyList<ReadingBucketAggregate>> GetBucketAggregatesAsync(
        string metricCode,
        DateTimeOffset from,
        DateTimeOffset to,
        ReadingBucket bucket,
        Guid? meterId,
        string? location,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = Measured(dbContext, metricCode, from, to);

        if (meterId is { } meter)
        {
            query = query.Where(reading => reading.MeterId == meter);
        }

        if (location is { Length: > 0 } locationFilter)
        {
            query = query.Where(reading => reading.Meter.Location == locationFilter);
        }

        if (bucket == ReadingBucket.Day)
        {
            return await query
                .GroupBy(reading => new
                {
                    reading.ObservedAt.Year,
                    reading.ObservedAt.Month,
                    reading.ObservedAt.Day,
                })
                .OrderBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Month)
                .ThenBy(group => group.Key.Day)
                .Select(group => new ReadingBucketAggregate(
                    string.Empty,
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    0,
                    group.Count(),
                    group.Min(reading => reading.ValueNumeric),
                    group.Max(reading => reading.ValueNumeric),
                    group.Average(reading => reading.ValueNumeric)))
                .ToListAsync(cancellationToken);
        }

        return await query
            .GroupBy(reading => new
            {
                reading.ObservedAt.Year,
                reading.ObservedAt.Month,
                reading.ObservedAt.Day,
                reading.ObservedAt.Hour,
            })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .ThenBy(group => group.Key.Day)
            .ThenBy(group => group.Key.Hour)
            .Select(group => new ReadingBucketAggregate(
                string.Empty,
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Key.Hour,
                group.Count(),
                group.Min(reading => reading.ValueNumeric),
                group.Max(reading => reading.ValueNumeric),
                group.Average(reading => reading.ValueNumeric)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReadingBucketAggregate>> GetLocationBucketAggregatesAsync(
        string metricCode,
        DateTimeOffset from,
        DateTimeOffset to,
        ReadingBucket bucket,
        IReadOnlyCollection<string> locations,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = Measured(dbContext, metricCode, from, to);

        if (locations.Count > 0)
        {
            query = query.Where(reading => locations.Contains(reading.Meter.Location));
        }

        if (bucket == ReadingBucket.Day)
        {
            return await query
                .GroupBy(reading => new
                {
                    reading.Meter.Location,
                    reading.ObservedAt.Year,
                    reading.ObservedAt.Month,
                    reading.ObservedAt.Day,
                })
                .OrderBy(group => group.Key.Location)
                .ThenBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Month)
                .ThenBy(group => group.Key.Day)
                .Select(group => new ReadingBucketAggregate(
                    group.Key.Location,
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    0,
                    group.Count(),
                    group.Min(reading => reading.ValueNumeric),
                    group.Max(reading => reading.ValueNumeric),
                    group.Average(reading => reading.ValueNumeric)))
                .ToListAsync(cancellationToken);
        }

        return await query
            .GroupBy(reading => new
            {
                reading.Meter.Location,
                reading.ObservedAt.Year,
                reading.ObservedAt.Month,
                reading.ObservedAt.Day,
                reading.ObservedAt.Hour,
            })
            .OrderBy(group => group.Key.Location)
            .ThenBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .ThenBy(group => group.Key.Day)
            .ThenBy(group => group.Key.Hour)
            .Select(group => new ReadingBucketAggregate(
                group.Key.Location,
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Key.Hour,
                group.Count(),
                group.Min(reading => reading.ValueNumeric),
                group.Max(reading => reading.ValueNumeric),
                group.Average(reading => reading.ValueNumeric)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocationReadingAggregate>> GetLocationAggregatesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metricCode,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.Readings.Where(reading =>
            reading.ObservedAt >= from &&
            reading.ObservedAt < to &&
            reading.ValueNumeric != null);

        if (metricCode is { Length: > 0 } metricCodeFilter)
        {
            query = query.Where(reading => reading.MetricCode == metricCodeFilter);
        }

        return await query
            .GroupBy(reading => new { reading.Meter.Location, reading.MetricCode })
            .OrderByDescending(group => group.Average(reading => reading.ValueNumeric))
            .ThenBy(group => group.Key.Location)
            .Select(group => new LocationReadingAggregate(
                group.Key.Location,
                group.Key.MetricCode,
                group.Count(),
                group.Min(reading => reading.ValueNumeric),
                group.Max(reading => reading.ValueNumeric),
                group.Average(reading => reading.ValueNumeric)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DbReading>> GetLatestAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var newest = dbContext.Readings
            .GroupBy(reading => new { reading.MeterId, reading.MetricCode })
            .Select(group => new
            {
                group.Key.MeterId,
                group.Key.MetricCode,
                ObservedAt = group.Max(reading => reading.ObservedAt),
            });

        var query =
            from reading in dbContext.Readings.Include(reading => reading.Meter)
            join key in newest
                on new { reading.MeterId, reading.MetricCode, reading.ObservedAt }
                equals new { key.MeterId, key.MetricCode, key.ObservedAt }
            select reading;

        if (location is { Length: > 0 } locationFilter)
        {
            query = query.Where(reading => reading.Meter.Location == locationFilter);
        }

        if (meterType is { Length: > 0 } meterTypeFilter)
        {
            query = query.Where(reading => reading.Meter.MeterType == meterTypeFilter);
        }

        return await query
            .OrderBy(reading => reading.Meter.Location)
            .ThenBy(reading => reading.MetricCode)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<DbReading> Measured(
        GatewayDbContext dbContext,
        string metricCode,
        DateTimeOffset from,
        DateTimeOffset to) =>
        dbContext.Readings.Where(reading =>
            reading.MetricCode == metricCode &&
            reading.ObservedAt >= from &&
            reading.ObservedAt < to &&
            reading.ValueNumeric != null);
}
