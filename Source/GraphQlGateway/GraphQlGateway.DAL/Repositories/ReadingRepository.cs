using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace GraphQlGateway.DAL.Repositories;

public sealed class ReadingRepository(IDbContextFactory<GatewayDbContext> dbContextFactory)
    : IReadingRepository
{
    public async Task<IReadOnlyList<HourlyReadingAggregate>> GetHourlyAggregatesAsync(
        string metricCode,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? meterId,
        string? location,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.Readings.Where(reading =>
            reading.MetricCode == metricCode &&
            reading.ObservedAt >= from &&
            reading.ObservedAt < to &&
            reading.ValueNumeric != null);

        if (meterId is { } meter)
        {
            query = query.Where(reading => reading.MeterId == meter);
        }

        if (location is { Length: > 0 } locationFilter)
        {
            query = query.Where(reading => reading.Meter.Location == locationFilter);
        }

        return await query
            .GroupBy(reading => new
            {
                reading.ObservedAt.Year,
                reading.ObservedAt.Month,
                reading.ObservedAt.Day,
                reading.ObservedAt.Hour,
            })
            .Select(group => new HourlyReadingAggregate(
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Key.Hour,
                group.Count(),
                group.Min(reading => reading.ValueNumeric),
                group.Max(reading => reading.ValueNumeric),
                group.Sum(reading => reading.ValueNumeric)))
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
}
