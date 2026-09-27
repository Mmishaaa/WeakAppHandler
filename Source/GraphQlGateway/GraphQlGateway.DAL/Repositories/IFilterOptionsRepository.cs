using GraphQlGateway.DAL.Models;

namespace GraphQlGateway.DAL.Repositories;

public interface IFilterOptionsRepository
{
    Task<FilterOptionsAggregate> GetAsync(CancellationToken cancellationToken);
}
