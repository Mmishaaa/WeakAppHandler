using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public interface IReadingRepository
{
    Task AddRangeAsync(IEnumerable<DbReading> readings, CancellationToken cancellationToken);

    Task<IReadOnlyList<DbReading>> GetByMeterAsync(
        Guid meterId,
        string? metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountByMeterAsync(
        Guid meterId,
        string? metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}
