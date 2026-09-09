using DataProcessorService.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataProcessorService.DAL.Repositories;

public sealed class MeterRepository(ProcessorDbContext dbContext) : IMeterRepository
{
    public async Task<IReadOnlyList<DbMeter>> GetByLocationsAsync(
        IReadOnlyCollection<string> locations,
        CancellationToken cancellationToken)
    {
        return await dbContext.Meters
            .Where(meter => locations.Contains(meter.Location))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DbMeter meter, CancellationToken cancellationToken) =>
        await dbContext.Meters.AddAsync(meter, cancellationToken);
}
