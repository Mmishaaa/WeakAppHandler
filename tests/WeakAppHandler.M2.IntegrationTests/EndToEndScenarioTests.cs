using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.M2.IntegrationTests;

/// <summary>
/// TASK-022: the M2 milestone's acceptance criteria (PRD §3.3 F1, F3) exercised through the real
/// Ingestor and the real Processor talking over a real broker into a real database — the seam no
/// prior task's tests cover, since WeakAppHandler.Ingestor.Tests only proves what the Ingestor
/// publishes and WeakAppHandler.Processor.Infrastructure.Tests only proves what the Processor does
/// with a hand-built message. Each test gets its own virtual host so two tests started concurrently
/// cannot see each other's deliveries; within this collection they still run sequentially (xUnit
/// serialises test classes sharing a collection fixture), so the shared database is never contended.
/// </summary>
[Collection(EndToEndCollectionDefinition.Name)]
public sealed class EndToEndScenarioTests(IntegrationTestFixture fixture)
{
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Poll_ReturnsHttpError502_RecordsHttpErrorOutcomeWithNoReadings()
    {
        var weakAppClient = new FakeWeakAppClient(
            TestMeters.Failure(IngestOutcome.HttpError, httpStatus: 502, errorMessage: "Bad Gateway"));

        await RunScenarioAsync(weakAppClient, async (context, attempt) =>
        {
            var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == attempt.BatchId);

            Assert.Equal(IngestBatchOutcome.HttpError, batch.Outcome);
            Assert.Equal(502, batch.HttpStatus);
            Assert.Equal(0, batch.ReadingCount);
            Assert.False(await context.Readings.AnyAsync(r => r.BatchId == attempt.BatchId));
        });
    }

    [Fact]
    public async Task Poll_ReturnsRateLimited429_RecordsRateLimitedOutcome()
    {
        var weakAppClient = new FakeWeakAppClient(
            TestMeters.Failure(IngestOutcome.RateLimited, httpStatus: 429, errorMessage: "Too Many Requests"));

        await RunScenarioAsync(weakAppClient, async (context, attempt) =>
        {
            var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == attempt.BatchId);

            Assert.Equal(IngestBatchOutcome.RateLimited, batch.Outcome);
            Assert.Equal(429, batch.HttpStatus);
            Assert.Equal(0, batch.ReadingCount);
            Assert.False(await context.Readings.AnyAsync(r => r.BatchId == attempt.BatchId));
        });
    }

    [Fact]
    public async Task Poll_ReturnsCorruptedPayload_RecordsCorruptedOutcome()
    {
        var weakAppClient = new FakeWeakAppClient(
            TestMeters.Failure(IngestOutcome.Corrupted, httpStatus: null, errorMessage: "Response body could not be parsed as JSON."));

        await RunScenarioAsync(weakAppClient, async (context, attempt) =>
        {
            var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == attempt.BatchId);

            Assert.Equal(IngestBatchOutcome.Corrupted, batch.Outcome);
            Assert.Null(batch.HttpStatus);
            Assert.Equal(0, batch.ReadingCount);
            Assert.False(await context.Readings.AnyAsync(r => r.BatchId == attempt.BatchId));
        });
    }

    [Fact]
    public async Task Poll_MissingApiKey_RecordsUnauthorizedOutcome()
    {
        var weakAppClient = new FakeWeakAppClient(
            TestMeters.Failure(IngestOutcome.Unauthorized, httpStatus: 401, errorMessage: "Invalid or missing API key"));

        await RunScenarioAsync(weakAppClient, async (context, attempt) =>
        {
            var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == attempt.BatchId);

            Assert.Equal(IngestBatchOutcome.Unauthorized, batch.Outcome);
            Assert.Equal(401, batch.HttpStatus);
            Assert.Equal(0, batch.ReadingCount);
            Assert.False(await context.Readings.AnyAsync(r => r.BatchId == attempt.BatchId));
        });
    }

    [Fact]
    public async Task DuplicateDelivery_RedeliveredReadingsMessage_WritesReadingsOnce()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
            var weakAppClient = new FakeWeakAppClient(
                TestMeters.Success([TestMeters.Meter("energy", "Office-Duplicate", """{"energy":405}""")]));

            // The Processor's queues must exist before the Ingestor's immediate first poll publishes:
            // a topic exchange drops a message for any binding that does not exist yet, so starting
            // the Ingestor first would race the Processor's own startup and could silently lose it.
            await using var processor = await ProcessorEndToEndHost.StartAsync(fixture, virtualHost);
            await using var ingestor = await IngestorEndToEndHost.StartAsync(fixture.RabbitMq, virtualHost, weakAppClient);

            var readings = await ingestor.Ingested.WaitForAsync(_ => true, ConsumeTimeout);
            await processor.Consumed.WaitForConsumeCountAsync(readings.MessageId, expected: 1, ConsumeTimeout);

            // Simulates the at-least-once redelivery F3 requires tolerance for: the exact message
            // the Processor already consumed once, delivered to it a second time.
            await processor.Bus.Publish(readings);
            await processor.Consumed.WaitForConsumeCountAsync(readings.MessageId, expected: 2, ConsumeTimeout);

            Assert.Equal(1, await context.IngestBatches.CountAsync(b => b.Id == readings.BatchId));
            Assert.Equal(1, await context.Readings.CountAsync(r => r.BatchId == readings.BatchId));
            Assert.Equal(1, processor.Stats.Snapshot().Deduplicated);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task EighteenMetricBatch_IsFullyPersistedWellWithinThePollingInterval()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);
            var weakAppClient = new FakeWeakAppClient(TestMeters.Success(TestMeters.ObservedResponse));

            // See DuplicateDelivery_RedeliveredReadingsMessage_WritesReadingsOnce for why the
            // Processor must be started before the Ingestor.
            await using var processor = await ProcessorEndToEndHost.StartAsync(fixture, virtualHost);
            await using var ingestor = await IngestorEndToEndHost.StartAsync(fixture.RabbitMq, virtualHost, weakAppClient);

            var readings = await ingestor.Ingested.WaitForAsync(m => m.Readings.Count == 18, ConsumeTimeout);
            await processor.Consumed.WaitForConsumeCountAsync(readings.MessageId, expected: 1, ConsumeTimeout);

            // Measured from the instant WeakApp's (faked) response was received, not from host
            // startup, so container/broker connect overhead cannot make an otherwise-fast pipeline
            // look slow. The default polling interval (WeakApp:PollingIntervalSeconds) is 10s.
            var elapsed = DateTimeOffset.UtcNow - readings.FetchedAt;
            var pollingInterval = TimeSpan.FromSeconds(10);

            Assert.True(
                elapsed < pollingInterval,
                $"Processing an 18-meter batch took {elapsed}, which does not leave a margin under the {pollingInterval} polling interval.");

            var batch = await context.IngestBatches.AsNoTracking().SingleAsync(b => b.Id == readings.BatchId);
            Assert.Equal(IngestBatchOutcome.Success, batch.Outcome);
            Assert.Equal(18, batch.ReadingCount);
            Assert.Equal(TestMeters.ObservedResponseReadingCount, await context.Readings.CountAsync(r => r.BatchId == readings.BatchId));
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    /// <summary>
    /// The four F1 failure-outcome scenarios share everything except the fake client's script and the
    /// resulting assertion: start both real services on a fresh virtual host, wait for the attempt the
    /// real poll produced, wait for the real Processor to have consumed it, then let the caller check
    /// <c>ingest_batches</c>.
    /// </summary>
    private async Task RunScenarioAsync(
        FakeWeakAppClient weakAppClient,
        Func<Processor.Infrastructure.Persistence.CoreDbContext, IngestAttemptRecorded, Task> assert)
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            await using var context = await ProcessorDatabase.CreateMigratedContextAsync(fixture);

            // See DuplicateDelivery_RedeliveredReadingsMessage_WritesReadingsOnce for why the
            // Processor must be started before the Ingestor.
            await using var processor = await ProcessorEndToEndHost.StartAsync(fixture, virtualHost);
            await using var ingestor = await IngestorEndToEndHost.StartAsync(fixture.RabbitMq, virtualHost, weakAppClient);

            var attempt = await ingestor.Attempts.WaitForAsync(_ => true, ConsumeTimeout);
            await processor.Consumed.WaitForConsumeCountAsync(attempt.MessageId, expected: 1, ConsumeTimeout);

            await assert(context, attempt);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }
}
