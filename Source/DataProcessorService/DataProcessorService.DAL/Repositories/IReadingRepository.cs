using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public interface IReadingRepository
{
    Task AddRangeAsync(IEnumerable<DbReading> readings, CancellationToken cancellationToken);
}
