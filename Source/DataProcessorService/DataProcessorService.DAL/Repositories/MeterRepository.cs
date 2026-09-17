using Microsoft.EntityFrameworkCore;
using Shared.Entities;

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

    public async Task<IReadOnlyList<DbMeter>> GetAllAsync(
        string? location,
        string? meterType,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Meters.AsNoTracking();

        if (location is { Length: > 0 })
        {
            query = query.Where(meter => meter.Location == location);
        }

        if (meterType is { Length: > 0 })
        {
            query = query.Where(meter => meter.MeterType == meterType);
        }

        return await query
            .OrderBy(meter => meter.Location)
            .ThenBy(meter => meter.MeterType)
            .ToListAsync(cancellationToken);
    }

    public async Task<DbMeter?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Meters
            .AsNoTracking()
            .FirstOrDefaultAsync(meter => meter.Id == id, cancellationToken);

    public async Task AddAsync(DbMeter meter, CancellationToken cancellationToken) =>
        await dbContext.Meters.AddAsync(meter, cancellationToken);
}
