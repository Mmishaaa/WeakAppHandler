using GraphQlGateway.BLL.Models;
using GraphQlGateway.BLL.Projections;
using GraphQlGateway.DAL.Repositories;
using Microsoft.Extensions.Options;
using Shared.Configuration;

namespace GraphQlGateway.BLL.Services;

public sealed class ReadingStatsService(
    IReadingRepository readingRepository,
    IOptionsMonitor<ThresholdOptions> thresholds)
    : IReadingStatsService
{
    public async Task<IReadOnlyList<ReadingBucketModel>> GetTimeBucketsAsync(
        ReadingStatsModel input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var aggregates = await readingRepository.GetBucketAggregatesAsync(
            input.MetricCode,
            input.From,
            input.To,
            ToBucket(input.Bucket),
            input.MeterId,
            input.Location,
            cancellationToken);

        return [.. aggregates.Select(ToBucketModel)];
    }

    public async Task<IReadOnlyList<LocationSeriesModel>> GetLocationSeriesAsync(
        ReadingStatsModel input,
        IReadOnlyList<string>? locations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var aggregates = await readingRepository.GetLocationBucketAggregatesAsync(
            input.MetricCode,
            input.From,
            input.To,
            ToBucket(input.Bucket),
            locations ?? [],
            cancellationToken);

        return
        [
            .. aggregates
                .GroupBy(aggregate => aggregate.Location, StringComparer.Ordinal)
                .Select(group => new LocationSeriesModel(
                    group.Key,
                    [.. group.Select(ToBucketModel)])),
        ];
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
                aggregate.TrueCount,
                aggregate.TrueShare,
                aggregate.Min,
                aggregate.Max,
                aggregate.Average)),
        ];
    }

    public async Task<IReadOnlyList<MeterTypeStatModel>> GetMeterTypeStatsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metricCode,
        CancellationToken cancellationToken)
    {
        var aggregates = await readingRepository.GetMeterTypeAggregatesAsync(
            from,
            to,
            metricCode,
            cancellationToken);

        return
        [
            .. aggregates.Select(aggregate => new MeterTypeStatModel(
                aggregate.MeterType,
                aggregate.MetricCode,
                aggregate.Count,
                aggregate.TrueCount,
                aggregate.TrueShare,
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

    public async Task<IReadOnlyList<MetricSnapshotModel>> GetMetricSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var readings = await readingRepository.GetLatestAsync(null, null, cancellationToken);

        var configured = thresholds.CurrentValue.Metrics;

        return
        [
            .. readings
                .GroupBy(reading => reading.MetricCode, StringComparer.Ordinal)
                .Select(group => ToSnapshot(group.Key, [.. group], configured))
                .OrderBy(snapshot => snapshot.MetricCode, StringComparer.Ordinal),
        ];
    }

    private static MetricSnapshotModel ToSnapshot(
        string metricCode,
        IReadOnlyList<Shared.Entities.DbReading> readings,
        IReadOnlyList<MetricThresholdOptions> configured)
    {
        var threshold = configured.FirstOrDefault(entry =>
            string.Equals(entry.MetricCode, metricCode, StringComparison.OrdinalIgnoreCase));

        var leading = readings
            .OrderByDescending(reading => reading.ValueNumeric ?? decimal.MinValue)
            .ThenByDescending(reading => reading.ObservedAt)
            .First();

        var (state, breached) = Evaluate(leading.ValueNumeric, threshold);

        return new MetricSnapshotModel(
            metricCode,
            threshold?.Unit ?? string.Empty,
            leading.Meter.Location,
            leading.Meter.MeterType,
            leading.ObservedAt,
            leading.ValueNumeric,
            leading.ValueBool,
            readings.Count,
            state,
            breached);
    }

    private static (MetricState State, decimal? Threshold) Evaluate(
        decimal? value,
        MetricThresholdOptions? threshold)
    {
        if (value is not { } measured || threshold is null)
        {
            return (MetricState.Unknown, null);
        }

        if (threshold.Min is { } min && measured < min)
        {
            return (MetricState.Below, min);
        }

        if (threshold.Max is { } max && measured > max)
        {
            return (MetricState.Above, max);
        }

        return (MetricState.Ok, threshold.Max ?? threshold.Min);
    }

    private static ReadingBucketModel ToBucketModel(ReadingBucketAggregate aggregate) =>
        new(
            new DateTimeOffset(
                aggregate.Year,
                aggregate.Month,
                aggregate.Day,
                aggregate.Hour,
                0,
                0,
                TimeSpan.Zero),
            aggregate.Count,
            aggregate.TrueCount,
            aggregate.TrueShare,
            aggregate.Min,
            aggregate.Max,
            aggregate.Average);

    private static ReadingBucket ToBucket(TimeBucket bucket) =>
        bucket == TimeBucket.Day ? ReadingBucket.Day : ReadingBucket.Hour;
}
