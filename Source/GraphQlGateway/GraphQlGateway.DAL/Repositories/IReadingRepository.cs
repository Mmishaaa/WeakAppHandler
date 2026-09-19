using Shared.Entities;

namespace GraphQlGateway.DAL.Repositories;

public interface IReadingRepository
{
    Task<IReadOnlyList<ReadingBucketAggregate>> GetBucketAggregatesAsync(
        string metricCode,
        DateTimeOffset from,
        DateTimeOffset to,
        ReadingBucket bucket,
        Guid? meterId,
        string? location,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReadingBucketAggregate>> GetLocationBucketAggregatesAsync(
        string metricCode,
        DateTimeOffset from,
        DateTimeOffset to,
        ReadingBucket bucket,
        IReadOnlyCollection<string> locations,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LocationReadingAggregate>> GetLocationAggregatesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metricCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MeterTypeReadingAggregate>> GetMeterTypeAggregatesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metricCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DbReading>> GetLatestAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken);
}
