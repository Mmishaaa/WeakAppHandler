using Microsoft.EntityFrameworkCore;

namespace Shared.UnitOfWork;

public sealed class UnitOfWorkService(DbContext dbContext) : IUnitOfWorkService
{
    public async Task<IUnitOfWorkServiceScope> CreateScopeAsync(CancellationToken cancellationToken = default)
    {
        var currentTransaction = dbContext.Database.CurrentTransaction;

        if (currentTransaction is not null)
        {
            return new UnitOfWorkServiceScope(currentTransaction, dbContext, ownsTransaction: false);
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        return new UnitOfWorkServiceScope(transaction, dbContext, ownsTransaction: true);
    }
}
