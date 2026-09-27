using GraphQlGateway.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphQlGateway.DAL.Repositories;

public sealed class FilterOptionsRepository(IDbContextFactory<GatewayDbContext> dbContextFactory)
    : IFilterOptionsRepository
{
    public async Task<FilterOptionsAggregate> GetAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var locations = await dbContext.Meters
            .Select(meter => meter.Location)
            .Distinct()
            .OrderBy(location => location)
            .ToListAsync(cancellationToken);

        var meterTypes = await dbContext.Meters
            .Select(meter => meter.MeterType)
            .Distinct()
            .OrderBy(meterType => meterType)
            .ToListAsync(cancellationToken);

        var metricCodes = await dbContext.Readings
            .Select(reading => reading.MetricCode)
            .Distinct()
            .OrderBy(metricCode => metricCode)
            .ToListAsync(cancellationToken);

        return new FilterOptionsAggregate(locations, meterTypes, metricCodes);
    }
}
