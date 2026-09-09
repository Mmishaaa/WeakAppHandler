using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Shared.UnitOfWork;

public sealed class UnitOfWorkServiceScope(
    IDbContextTransaction transaction,
    DbContext dbContext,
    bool ownsTransaction)
    : IUnitOfWorkServiceScope
{
    private bool _committed;
    private bool _disposed;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_committed)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (ownsTransaction)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        _committed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (ownsTransaction)
        {
            transaction.Dispose();
        }

        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (ownsTransaction)
        {
            await transaction.DisposeAsync();
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
