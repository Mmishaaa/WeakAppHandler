namespace Shared.UnitOfWork;

public interface IUnitOfWorkService
{
    Task<IUnitOfWorkServiceScope> CreateScopeAsync(CancellationToken cancellationToken = default);
}
