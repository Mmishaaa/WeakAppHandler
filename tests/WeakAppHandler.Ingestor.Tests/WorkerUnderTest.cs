using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Ingestor.Polling;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// Ties an <see cref="IngestionWorker"/> to the provider whose scopes it opens, so a test can stop
/// and dispose both together without leaving a background loop running past the test that made it.
/// </summary>
internal sealed class WorkerUnderTest(IngestionWorker worker, ServiceProvider provider) : IAsyncDisposable
{
    public Task StartAsync(CancellationToken cancellationToken) => worker.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => worker.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        worker.Dispose();
        await provider.DisposeAsync().ConfigureAwait(false);
    }
}
