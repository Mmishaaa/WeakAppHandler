using GraphQlGateway.BLL.Models;
using GraphQlGateway.DAL.Repositories;

namespace GraphQlGateway.BLL.Services;

public sealed class FilterOptionsService(IFilterOptionsRepository filterOptionsRepository)
    : IFilterOptionsService
{
    public async Task<FilterOptionsModel> GetAsync(CancellationToken cancellationToken)
    {
        var options = await filterOptionsRepository.GetAsync(cancellationToken);

        return new FilterOptionsModel(options.Locations, options.MeterTypes, options.MetricCodes);
    }
}
