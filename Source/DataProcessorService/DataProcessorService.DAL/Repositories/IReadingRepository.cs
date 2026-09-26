using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public interface IReadingRepository
{
    Task AddRangeAsync(IEnumerable<DbReading> readings, CancellationToken cancellationToken);

    Task<ReadingPageAggregate> GetPageByMeterAsync(
        Guid meterId,
        string? metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
