using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-018's test steps end to end, through a real broker rather than by calling the recorder
/// directly: publishing the same message twice leaves one row, and a failed attempt leaves a batch
/// with no readings. This is also what proves the consumers are wired to the recorder at all — the
/// recorder-level tests would pass just as happily with the consumers unregistered. Each test gets
/// its own virtual host so two tests started concurrently cannot see each other's deliveries.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class IngestionConsumerTests(IntegrationTestFixture fixture)
{
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ReadingsIngestedConsumer_ReceivingTheSameMessageTwice_WritesOneBatchAndOneSetOfReadings()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
            await using var host = await ProcessorHost.StartAsync(fixture, virtualHost);

            var batchId = Guid.NewGuid();
            var message = IngestionMessages.Readings(batchId, "consumer-duplicate", meterCount: 2);

            await host.Bus.Publish(message);
            await host.Bus.Publish(message);

            // Both deliveries really reached the consumer; the deduplication is the recorder's, not
            // the transport quietly dropping the second one.
            await host.Consumed.WaitForConsumeCountAsync(message.MessageId, expected: 2, ConsumeTimeout);

            var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == batchId);

            Assert.Equal(IngestBatchOutcome.Success, batch.Outcome);
            Assert.Equal(2, batch.ReadingCount);
            Assert.Equal(2, await context.Readings.CountAsync(r => r.BatchId == batchId));
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task IngestAttemptRecordedConsumer_FailedAttempt_WritesABatchWithNoReadings()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
            await using var host = await ProcessorHost.StartAsync(fixture, virtualHost);

            var batchId = Guid.NewGuid();
            var message = IngestionMessages.Attempt(
                batchId,
                IngestOutcome.HttpError,
                readingCount: 0,
                httpStatus: 503,
                errorMessage: "WeakApp returned 503.");

            await host.Bus.Publish(message);

            await host.Consumed.WaitForConsumeCountAsync(message.MessageId, expected: 1, ConsumeTimeout);

            var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == batchId);

            Assert.Equal(IngestBatchOutcome.HttpError, batch.Outcome);
            Assert.Equal(503, batch.HttpStatus);
            Assert.Equal(0, batch.ReadingCount);
            Assert.False(await context.Readings.AnyAsync(r => r.BatchId == batchId));
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }
}
