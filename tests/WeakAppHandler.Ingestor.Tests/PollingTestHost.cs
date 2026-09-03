using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.Polling;
using WeakAppHandler.Ingestor.Telemetry;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// Hosts the real <see cref="IngestionPoller"/> over MassTransit's in-memory test harness, so what
/// the tests observe is what a bus was genuinely asked to publish rather than calls recorded by a
/// hand-written double. The broker-side half — that these publishes land on the right queues of the
/// real topology — is covered separately by <see cref="IngestionPublishingTests"/>.
/// </summary>
internal sealed class PollingTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private PollingTestHost(ServiceProvider provider, ITestHarness harness)
    {
        _provider = provider;
        Harness = harness;
    }

    public ITestHarness Harness { get; }

    public static async Task<PollingTestHost> StartAsync(IWeakAppClient weakAppClient)
    {
        var provider = new ServiceCollection()
            .AddSingleton(weakAppClient)
            .AddSingleton(TimeProvider.System)
            .AddSingleton<CircuitBreakerStateProvider>()
            .AddSingleton<IngestorMetrics>()
            .AddScoped<IIngestionPoller, IngestionPoller>()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return new PollingTestHost(provider, harness);
    }

    /// <summary>
    /// Resolves the poller the way the worker does — from a fresh scope — so a mistake such as
    /// capturing the scoped publish endpoint in a singleton surfaces here rather than at runtime.
    /// </summary>
    public async Task<IngestAttemptRecorded> PollOnceAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var poller = scope.ServiceProvider.GetRequiredService<IIngestionPoller>();
        return await poller.PollOnceAsync(CancellationToken.None);
    }

    /// <summary>
    /// The single message of this type the bus was asked to publish. The harness types the publish
    /// context as nullable, so the assertion here is what turns "nothing was published" into a
    /// readable failure instead of a NullReferenceException at the call site.
    /// </summary>
    public TMessage SinglePublished<TMessage>()
        where TMessage : class
    {
        var context = Harness.Published.Select<TMessage>().Single().Context;
        Assert.NotNull(context);
        return context.Message;
    }

    public async ValueTask DisposeAsync()
    {
        await Harness.Stop();
        await _provider.DisposeAsync();
    }
}
