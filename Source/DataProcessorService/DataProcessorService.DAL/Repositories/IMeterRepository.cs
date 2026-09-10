using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public interface IMeterRepository
{
    Task<IReadOnlyList<DbMeter>> GetByLocationsAsync(
        IReadOnlyCollection<string> locations,
        CancellationToken cancellationToken);

    Task AddAsync(DbMeter meter, CancellationToken cancellationToken);
}
