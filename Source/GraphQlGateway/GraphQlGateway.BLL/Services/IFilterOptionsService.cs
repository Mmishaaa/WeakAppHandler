using GraphQlGateway.BLL.Models;

namespace GraphQlGateway.BLL.Services;

public interface IFilterOptionsService
{
    Task<FilterOptionsModel> GetAsync(CancellationToken cancellationToken);
}
