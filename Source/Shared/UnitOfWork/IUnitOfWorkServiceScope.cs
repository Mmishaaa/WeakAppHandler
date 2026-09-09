namespace Shared.UnitOfWork;

public interface IUnitOfWorkServiceScope : IDisposable, IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
