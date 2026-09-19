using GraphQlGateway.API.Errors;
using GraphQlGateway.API.Schema.Filters;
using GraphQlGateway.BLL.Models;
using GraphQlGateway.BLL.Projections;
using GraphQlGateway.BLL.Services;
using GraphQlGateway.DAL;

namespace GraphQlGateway.API.Schema;

public sealed class Query
{
    [UseFiltering(typeof(MeterFilterType))]
    [UseSorting]
    public IQueryable<MeterModel> GetMeters(GatewayDbContext dbContext) =>
        dbContext.Meters.ToModels();

    [HandlePagingErrors]
    [UsePaging(MaxPageSize = 200, DefaultPageSize = 50, IncludeTotalCount = true)]
    public IQueryable<ReadingModel> GetReadings(
        GatewayDbContext dbContext,
        ReadingFilterModel? filter = null) =>
        dbContext.Readings
            .ApplyFilter(filter)
            .ToModels()
            .OrderByDescending(reading => reading.ObservedAt)
            .ThenByDescending(reading => reading.Id);

    public async Task<FilterOptionsModel> GetFilterOptionsAsync(
        IFilterOptionsService filterOptionsService,
        CancellationToken cancellationToken) =>
        await filterOptionsService.GetAsync(cancellationToken);

    public async Task<IReadOnlyList<ReadingModel>> GetLatestReadingsAsync(
        IReadingStatsService readingStatsService,
        CancellationToken cancellationToken,
        string? location = null,
        string? meterType = null) =>
        await readingStatsService.GetLatestReadingsAsync(location, meterType, cancellationToken);

    public async Task<IReadOnlyList<MetricSnapshotModel>> GetMetricSnapshotAsync(
        IReadingStatsService readingStatsService,
        CancellationToken cancellationToken) =>
        await readingStatsService.GetMetricSnapshotAsync(cancellationToken);

    public async Task<IReadOnlyList<ReadingBucketModel>> GetReadingStatsAsync(
        ReadingStatsModel input,
        IReadingStatsService readingStatsService,
        CancellationToken cancellationToken) =>
        await readingStatsService.GetTimeBucketsAsync(input, cancellationToken);

    public async Task<IReadOnlyList<LocationSeriesModel>> GetReadingSeriesAsync(
        ReadingStatsModel input,
        IReadingStatsService readingStatsService,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? locations = null) =>
        await readingStatsService.GetLocationSeriesAsync(input, locations, cancellationToken);

    public async Task<IReadOnlyList<MeterTypeStatModel>> GetMeterTypeStatsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadingStatsService readingStatsService,
        CancellationToken cancellationToken,
        string? metricCode = null) =>
        await readingStatsService.GetMeterTypeStatsAsync(from, to, metricCode, cancellationToken);

    public async Task<IReadOnlyList<LocationStatModel>> GetLocationStatsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadingStatsService readingStatsService,
        CancellationToken cancellationToken,
        string? metricCode = null) =>
        await readingStatsService.GetLocationStatsAsync(from, to, metricCode, cancellationToken);
}
