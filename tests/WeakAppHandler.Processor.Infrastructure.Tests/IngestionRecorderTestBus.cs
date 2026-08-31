using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Infrastructure.Ingestion;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// An in-memory bus backing <see cref="IngestionRecorder"/>'s <see cref="IPublishEndpoint"/> for
/// tests that construct the recorder directly against a real database rather than through
/// <see cref="ProcessorHost"/>'s real broker. What <see cref="IngestionRecorderTests"/> exercises is
/// the transaction, the ledger and the publish-after-commit ordering; that <see cref="ReadingStored"/>
/// actually reaches a real broker end to end is <see cref="IngestionConsumerTests"/>'s job.
/// </summary>
internal sealed class IngestionRecorderTestBus : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    private IngestionRecorderTestBus(ServiceProvider provider, AsyncServiceScope scope, ITestHarness harness)
    {
        _provider = provider;
        _scope = scope;
        Harness = harness;
    }

    public ITestHarness Harness { get; }

    // IPublishEndpoint is a scoped service (it is everywhere else in this codebase too — a consumer
    // resolves it from the delivery's own scope), so it cannot come from the root provider once
    // BuildServiceProvider(validateScopes: true) is enforcing that.
    public IPublishEndpoint PublishEndpoint => _scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

    public static async Task<IngestionRecorderTestBus> StartAsync()
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return new IngestionRecorderTestBus(provider, provider.CreateAsyncScope(), harness);
    }

    public async ValueTask DisposeAsync()
    {
        await Harness.Stop();
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
