using Shared.Entities;

namespace GraphQlGateway.DAL.Repositories;

public interface IReadingRepository
{
    Task<IReadOnlyList<HourlyReadingAggregate>> GetHourlyAggregatesAsync(
        string metricCode,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? meterId,
        string? location,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LocationReadingAggregate>> GetLocationAggregatesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metricCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DbReading>> GetLatestAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken);
}
