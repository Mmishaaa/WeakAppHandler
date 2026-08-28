using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// TASK-016 test step 1 and 2, run against a real broker rather than an in-memory bus: the messages
/// a poll produces have to reach the queues the shipped topology binds by routing key, not merely be
/// handed to a publish endpoint. <see cref="IngestionPollerTests"/> covers the message contents;
/// this covers the fact that they route.
/// </summary>
[Collection(IngestionCollectionDefinition.Name)]
public sealed class IngestionPublishingTests(RabbitMqIntegrationFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    // The queue names are fixed by the PRD, so a per-test vhost is what keeps one test's messages out
    // of another's assertions while still exercising the real names.
    private readonly string _virtualHost = $"task016-{Guid.NewGuid():N}";

    public Task InitializeAsync() => fixture.CreateVirtualHostAsync(_virtualHost);

    public Task DisposeAsync() => fixture.DeleteVirtualHostAsync(_virtualHost);

    [Fact]
    public async Task Polling_SuccessfulResponse_DeliversBothMessagesToTheirOwnQueuesWithOneBatchId()
    {
        var client = new FakeWeakAppClient(TestMeters.Success(TestMeters.ObservedResponse));
        await using var host = await IngestorHost.StartAsync(fixture, _virtualHost, client);

        // Waiting on readings.ingested first and then matching readings.attempt by batch id, rather
        // than taking whichever message happens to be first in each collector: the loop keeps running
        // while the assertions do, so pairing has to be explicit.
        var ingested = await host.Ingested.WaitForAsync(_ => true, Timeout);
        var attempt = await host.Attempts.WaitForAsync(a => a.BatchId == ingested.BatchId, Timeout);

        Assert.Equal(IngestOutcome.Success, attempt.Outcome);
        Assert.Equal(200, attempt.HttpStatus);
        Assert.Equal(TestMeters.ObservedResponse.Count, attempt.ReadingCount);
        Assert.Equal(TestMeters.ObservedResponse.Count, ingested.Readings.Count);
        Assert.Contains(ingested.Readings, r => r.Location == "Corridor" && r.MeterType == "air_quality");
    }

    [Fact]
    public async Task Polling_CorruptedResponse_DeliversOnlyTheAttemptRecord()
    {
        var client = new FakeWeakAppClient(
            TestMeters.Failure(IngestOutcome.Corrupted, 200, "Error while copying content to a stream"));
        await using var host = await IngestorHost.StartAsync(fixture, _virtualHost, client);

        var attempt = await host.Attempts.WaitForAsync(a => a.Outcome == IngestOutcome.Corrupted, Timeout);

        Assert.Equal(0, attempt.ReadingCount);

        // The attempt record has already been delivered, which means anything published ahead of it
        // in the same poll would have been routed by now too — so an empty readings queue here is a
        // real absence rather than a race.
        Assert.Empty(host.Ingested.Snapshot());
    }
}
