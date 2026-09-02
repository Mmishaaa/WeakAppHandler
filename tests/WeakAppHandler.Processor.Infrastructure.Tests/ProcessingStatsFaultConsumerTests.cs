using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Contracts;
using WeakAppHandler.Processor.Application.Stats;
using WeakAppHandler.Processor.Infrastructure.Ingestion;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-021's dead-letter counting, against MassTransit's in-memory test transport rather than the
/// real broker: what is under test is that <see cref="ReadingsIngestedFaultConsumer"/> and
/// <see cref="IngestAttemptRecordedFaultConsumer"/> are wired to <see cref="ProcessingStatsState"/> at
/// all, not the RabbitMQ retry/dead-letter mechanics <see cref="WeakAppHandler.Messaging.Tests"/>
/// already covers (TASK-012) or the recorder's own transaction (<see cref="IngestionConsumerTests"/>).
/// A throwing stand-in consumer stands in for a poisoned message, since MassTransit publishes
/// <see cref="Fault{T}"/> after any consumer exception regardless of what actually failed.
/// </summary>
public sealed class ProcessingStatsFaultConsumerTests
{
    private static readonly TimeSpan HarnessTimeoutDuration = TimeSpan.FromSeconds(10);

    private static CancellationToken HarnessTimeout => new CancellationTokenSource(HarnessTimeoutDuration).Token;

    [Fact]
    public async Task ReadingsIngestedFaultConsumer_ConsumerFault_IncrementsDeadLetteredOnly()
    {
        var stats = new ProcessingStatsState();

        await using var provider = new ServiceCollection()
            .AddSingleton(stats)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<AlwaysThrowingConsumer>();
                cfg.AddConsumer<ReadingsIngestedFaultConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new ReadingsIngested(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 10, []));

        Assert.True(
            await harness.Published.Any<Fault<ReadingsIngested>>(HarnessTimeout),
            "Expected MassTransit to publish a Fault<ReadingsIngested> after the throwing consumer failed.");
        Assert.True(
            await harness.Consumed.Any<Fault<ReadingsIngested>>(HarnessTimeout),
            "Expected ReadingsIngestedFaultConsumer to consume the fault event.");

        var snapshot = stats.Snapshot();
        Assert.Equal(0, snapshot.Processed);
        Assert.Equal(0, snapshot.Deduplicated);
        Assert.Equal(1, snapshot.DeadLettered);
    }

    [Fact]
    public async Task IngestAttemptRecordedFaultConsumer_ConsumerFault_IncrementsDeadLetteredOnly()
    {
        var stats = new ProcessingStatsState();

        await using var provider = new ServiceCollection()
            .AddSingleton(stats)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<AlwaysThrowingAttemptConsumer>();
                cfg.AddConsumer<IngestAttemptRecordedFaultConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new IngestAttemptRecorded(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, IngestOutcome.HttpError, 503, 20, 0, "boom"));

        Assert.True(
            await harness.Consumed.Any<Fault<IngestAttemptRecorded>>(HarnessTimeout),
            "Expected IngestAttemptRecordedFaultConsumer to consume the fault event.");

        var snapshot = stats.Snapshot();
        Assert.Equal(1, snapshot.DeadLettered);
    }

    /// <summary>Stands in for a poisoned <see cref="ReadingsIngested"/> delivery: always throws.</summary>
    private sealed class AlwaysThrowingConsumer : IConsumer<ReadingsIngested>
    {
        public Task Consume(ConsumeContext<ReadingsIngested> context) =>
            throw new InvalidOperationException("Simulated poison message.");
    }

    /// <summary>Stands in for a poisoned <see cref="IngestAttemptRecorded"/> delivery: always throws.</summary>
    private sealed class AlwaysThrowingAttemptConsumer : IConsumer<IngestAttemptRecorded>
    {
        public Task Consume(ConsumeContext<IngestAttemptRecorded> context) =>
            throw new InvalidOperationException("Simulated poison message.");
    }
}
