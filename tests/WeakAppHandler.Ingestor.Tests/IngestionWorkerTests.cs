using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeakAppHandler.Ingestor.Polling;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// TASK-016's third acceptance criterion: when a poll runs longer than the interval the next cycle
/// is skipped rather than started alongside it. Driven with a real clock and a deliberately slow
/// poller — the alternative, a fake clock, has to be pumped forward by the test while the loop is
/// awaiting inside it, which is a synchronisation hazard rather than a simplification.
/// </summary>
public sealed class IngestionWorkerTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task ExecuteAsync_PollOutlastingTheInterval_SkipsACycleInsteadOfOverlappingPolls()
    {
        // 1.2s of work per 1s cycle. Skipping puts the next poll on the following grid point, 2s
        // after the last one started; catching up would restart it the instant the previous poll
        // finished, 1.2s later — which is what "overlapping polls" degenerates into.
        var poller = new RecordingIngestionPoller(TimeSpan.FromMilliseconds(1200));
        await using var worker = CreateWorker(poller);

        await worker.StartAsync(CancellationToken.None);
        await poller.WaitForCallsAsync(3, TimeSpan.FromSeconds(20));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, poller.MaxConcurrentPolls);

        var starts = poller.StartOffsets;
        for (var i = 1; i < starts.Count; i++)
        {
            var gap = starts[i] - starts[i - 1];
            var skippedACycle = gap >= TimeSpan.FromMilliseconds(1700);
            var failureMessage =
                $"Poll {i} started {gap.TotalMilliseconds:F0}ms after poll {i - 1}. A skipped cycle puts it " +
                "~2000ms later; anything close to the 1200ms poll duration means the loop caught up instead.";

            Assert.True(skippedACycle, failureMessage);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PollThrowing_KeepsTheLoopAliveForTheNextCycle()
    {
        // A poll can fail outside the resilience pipeline (a broker publish, for instance). The
        // service has to stay up and try again on the next tick rather than stopping silently.
        var poller = new RecordingIngestionPoller(TimeSpan.Zero, throwOnFirstCalls: 1);
        await using var worker = CreateWorker(poller);

        await worker.StartAsync(CancellationToken.None);
        await poller.WaitForCallsAsync(3, TimeSpan.FromSeconds(20));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(poller.CallCount >= 3);
    }

    private static WorkerUnderTest CreateWorker(IIngestionPoller poller)
    {
        var provider = new ServiceCollection()
            .AddScoped(_ => poller)
            .BuildServiceProvider(validateScopes: true);

        var options = Options.Create(new WeakAppOptions
        {
            BaseUrl = new Uri("http://weakapp.local"),
            ApiKey = "test-api-key",
            PollingIntervalSeconds = (int)Interval.TotalSeconds,
            AttemptTimeoutSeconds = 1,
            TotalTimeoutSeconds = 2,
        });

        var worker = new IngestionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            TimeProvider.System,
            NullLogger<IngestionWorker>.Instance);

        return new WorkerUnderTest(worker, provider);
    }
}
