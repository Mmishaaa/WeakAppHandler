using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public interface IMeterRepository
{
    Task<IReadOnlyList<DbMeter>> GetByLocationsAsync(
        IReadOnlyCollection<string> locations,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DbMeter>> GetAllAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken);

    Task<DbMeter?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(DbMeter meter, CancellationToken cancellationToken);
}
