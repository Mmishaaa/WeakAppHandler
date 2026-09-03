using System.Collections.Concurrent;
using System.Diagnostics;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.Telemetry;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// TASK-044: <see cref="Polling.IngestionPoller"/> starts a span on <see cref="IngestorActivitySource"/>
/// spanning the whole poll attempt, tagged with the batch id and outcome, so that MassTransit's own
/// publish spans - and the trace id they carry into message headers - nest under it instead of each
/// starting a fresh trace. The full hop into the Processor is covered by
/// WeakAppHandler.M2.IntegrationTests' tracing scenario; this proves the span itself is shaped right.
/// </summary>
public sealed class IngestionPollerTracingTests
{
    [Fact]
    public async Task PollOnceAsync_SuccessfulPoll_StartsASpanTaggedWithBatchIdAndOutcome()
    {
        using var activities = new ActivityCollector(IngestorActivitySource.Name);
        await using var host = await PollingTestHost.StartAsync(
            new FakeWeakAppClient(TestMeters.Success(TestMeters.ObservedResponse)));

        var attempt = await host.PollOnceAsync();

        // Filtered by batch id rather than Assert.Single: IngestorActivitySource.Instance is one
        // static ActivitySource shared by the whole process, so IngestionPollerTests' own polls -
        // running concurrently in xUnit's default per-class parallelism - land on this same listener
        // too. The batch id is the one thing that ties an activity back to this poll specifically.
        var activity = Assert.Single(
            activities.Activities,
            a => a.GetTagItem("weakapphandler.batch_id")?.ToString() == attempt.BatchId.ToString());
        Assert.Equal("weakapp.poll", activity.OperationName);
        Assert.Equal(ActivityKind.Producer, activity.Kind);
        Assert.Equal(nameof(IngestOutcome.Success), activity.GetTagItem("weakapphandler.poll.outcome")?.ToString());
        Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task PollOnceAsync_FailedPoll_MarksTheSpanAsError()
    {
        using var activities = new ActivityCollector(IngestorActivitySource.Name);
        await using var host = await PollingTestHost.StartAsync(
            new FakeWeakAppClient(TestMeters.Failure(IngestOutcome.HttpError, 502, "Bad Gateway")));

        var attempt = await host.PollOnceAsync();

        // See PollOnceAsync_SuccessfulPoll_StartsASpanTaggedWithBatchIdAndOutcome for why this
        // filters by batch id instead of assuming the collection holds exactly one activity.
        var activity = Assert.Single(
            activities.Activities,
            a => a.GetTagItem("weakapphandler.batch_id")?.ToString() == attempt.BatchId.ToString());
        Assert.Equal(nameof(IngestOutcome.HttpError), activity.GetTagItem("weakapphandler.poll.outcome")?.ToString());
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    /// <summary>
    /// A minimal <see cref="ActivityListener"/> rather than the full OTel SDK: sampling an
    /// <see cref="ActivitySource"/> at all requires some listener to be registered, and this is the
    /// standard way to observe what a source produced without an exporter in the loop.
    /// </summary>
    private sealed class ActivityCollector : IDisposable
    {
        private readonly ActivityListener _listener;

        public ActivityCollector(string sourceName)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = Activities.Add,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public ConcurrentBag<Activity> Activities { get; } = [];

        public void Dispose() => _listener.Dispose();
    }
}
