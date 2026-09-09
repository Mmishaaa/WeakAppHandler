using DataProcessorService.DAL.Entities;

namespace DataProcessorService.DAL.Repositories;

public sealed class ReadingRepository(ProcessorDbContext dbContext) : IReadingRepository
{
    public async Task AddRangeAsync(IEnumerable<DbReading> readings, CancellationToken cancellationToken) =>
        await dbContext.Readings.AddRangeAsync(readings, cancellationToken);
}
