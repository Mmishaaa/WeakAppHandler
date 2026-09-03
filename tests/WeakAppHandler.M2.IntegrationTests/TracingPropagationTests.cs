using System.Collections.Concurrent;
using System.Diagnostics;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.M2.IntegrationTests;

/// <summary>
/// TASK-044's core acceptance criterion (PRD §6 F10): one reading can be traced end to end, from the
/// Ingestor's poll through the real broker into the real Processor, on a single trace id. Proven with
/// a raw <see cref="ActivityListener"/> rather than the OTel SDK/an OTLP collector: sampling an
/// <see cref="ActivitySource"/> at all only requires some listener to be registered, and this is the
/// standard way to observe what a source produced without an exporter in the loop. The SDK's own
/// wildcard wiring ("WeakAppHandler.*"/"MassTransit" in ServiceDefaultsExtensions) is proven separately
/// in WeakAppHandler.ServiceDefaults.Tests; what this test proves is that MassTransit's header
/// propagation and the Ingestor's own wrapping span genuinely keep one trace id alive across the hop
/// from an HTTP poll into two independent publishes and back out through two real consumers.
/// </summary>
[Collection(EndToEndCollectionDefinition.Name)]
public sealed class TracingPropagationTests(IntegrationTestFixture fixture)
{
    /// <summary>Matches <c>WeakAppHandler.Ingestor.Telemetry.IngestorActivitySource.Name</c>, an
    /// internal type this project has no visibility into - duplicated as a literal per this test
    /// project's established convention of not sharing internal types across test assemblies.</summary>
    private const string IngestorSourceName = "WeakAppHandler.Ingestor";

    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Poll_SuccessfulReading_IsTraceableEndToEndOnASingleTraceId()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is IngestorSourceName or "MassTransit",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
            var weakAppClient = new FakeWeakAppClient(
                TestMeters.Success([TestMeters.Meter("energy", "Trace-Office", """{"energy":123}""")]));

            // See EndToEndScenarioTests for why the Processor must be started before the Ingestor:
            // its queues must be bound before the Ingestor's immediate first poll publishes.
            await using var processor = await ProcessorEndToEndHost.StartAsync(fixture, virtualHost);
            await using var ingestor = await IngestorEndToEndHost.StartAsync(fixture.RabbitMq, virtualHost, weakAppClient);

            var readings = await ingestor.Ingested.WaitForAsync(_ => true, ConsumeTimeout);
            await processor.Consumed.WaitForConsumeCountAsync(readings.MessageId, expected: 1, ConsumeTimeout);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }

        var pollActivity = Assert.Single(activities, a => a.Source.Name == IngestorSourceName);
        var massTransitActivities = activities.Where(a => a.Source.Name == "MassTransit").ToList();

        Assert.NotEmpty(massTransitActivities);
        Assert.All(
            activities,
            a => Assert.True(
                a.TraceId == pollActivity.TraceId,
                $"Activity '{a.OperationName}' (source {a.Source.Name}) had trace id {a.TraceId}, expected the poll's {pollActivity.TraceId}."));
    }
}
