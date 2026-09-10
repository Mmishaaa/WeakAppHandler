using GraphQlGateway.BLL.Models;

namespace GraphQlGateway.BLL.Services;

public interface IReadingStatsService
{
    Task<IReadOnlyList<ReadingBucketModel>> GetTimeBucketsAsync(
        ReadingStatsModel input,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LocationStatModel>> GetLocationStatsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metricCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReadingModel>> GetLatestReadingsAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken);
}
