using GraphQlGateway.BLL.Models;
using GraphQlGateway.BLL.Projections;
using GraphQlGateway.DAL.Repositories;

namespace GraphQlGateway.BLL.Services;

public sealed class ReadingStatsService(IReadingRepository readingRepository) : IReadingStatsService
{
    public async Task<IReadOnlyList<ReadingBucketModel>> GetTimeBucketsAsync(
        ReadingStatsModel input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var hours = await readingRepository.GetHourlyAggregatesAsync(
            input.MetricCode,
            input.From,
            input.To,
            input.MeterId,
            input.Location,
            cancellationToken);

        var buckets = input.Bucket == TimeBucket.Day
            ? hours
                .GroupBy(hour => new DateTimeOffset(hour.Year, hour.Month, hour.Day, 0, 0, 0, TimeSpan.Zero))
                .Select(group => new ReadingBucketModel(
                    group.Key,
                    group.Sum(hour => hour.Count),
                    group.Min(hour => hour.Min),
                    group.Max(hour => hour.Max),
                    Average(group.Sum(hour => hour.Sum), group.Sum(hour => hour.Count))))
            : hours
                .Select(hour => new ReadingBucketModel(
                    new DateTimeOffset(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0, TimeSpan.Zero),
                    hour.Count,
                    hour.Min,
                    hour.Max,
                    Average(hour.Sum, hour.Count)));

        return [.. buckets.OrderBy(bucket => bucket.BucketStart)];
    }

    public async Task<IReadOnlyList<LocationStatModel>> GetLocationStatsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metricCode,
        CancellationToken cancellationToken)
    {
        var aggregates = await readingRepository.GetLocationAggregatesAsync(
            from,
            to,
            metricCode,
            cancellationToken);

        return
        [
            .. aggregates.Select(aggregate => new LocationStatModel(
                aggregate.Location,
                aggregate.MetricCode,
                aggregate.Count,
                aggregate.Min,
                aggregate.Max,
                aggregate.Average)),
        ];
    }

    public async Task<IReadOnlyList<ReadingModel>> GetLatestReadingsAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken)
    {
        var readings = await readingRepository.GetLatestAsync(location, meterType, cancellationToken);

        return readings.ToModelList();
    }

    private static decimal? Average(decimal? sum, int count) =>
        sum is null || count == 0 ? null : sum / count;
}
