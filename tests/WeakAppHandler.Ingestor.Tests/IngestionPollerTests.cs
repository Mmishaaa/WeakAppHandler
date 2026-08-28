using System.Security.Cryptography;
using System.Text;
using MassTransit.Testing;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// TASK-016's first two acceptance criteria: every attempt publishes
/// <see cref="IngestAttemptRecorded"/>, and only a successful, well-formed response additionally
/// publishes <see cref="ReadingsIngested"/> carrying the same batch id.
/// </summary>
public sealed class IngestionPollerTests
{
    [Fact]
    public async Task PollOnceAsync_SuccessfulResponse_PublishesBothMessagesSharingOneBatchId()
    {
        await using var host = await PollingTestHost.StartAsync(
            new FakeWeakAppClient(TestMeters.Success(TestMeters.ObservedResponse)));

        var attempt = await host.PollOnceAsync();

        var ingested = host.SinglePublished<ReadingsIngested>();
        var recorded = host.SinglePublished<IngestAttemptRecorded>();

        Assert.Equal(IngestOutcome.Success, recorded.Outcome);
        Assert.Equal(attempt.BatchId, ingested.BatchId);
        Assert.Equal(attempt.BatchId, recorded.BatchId);
        Assert.Equal(TestMeters.ObservedResponse.Count, recorded.ReadingCount);
        Assert.Equal(TestMeters.ObservedResponse.Count, ingested.Readings.Count);

        // The two messages describe one attempt but are deduplicated independently downstream, so
        // they must not share the idempotency key they are deduplicated by.
        Assert.NotEqual(ingested.MessageId, recorded.MessageId);
    }

    [Fact]
    public async Task PollOnceAsync_SuccessfulResponse_MapsEachMeterToAnEnvelopeWithItsRawPayloadAndHash()
    {
        var meter = TestMeters.Meter("air_quality", "Corridor", """{"co2":727,"pm25":42,"humidity":47}""");
        await using var host = await PollingTestHost.StartAsync(new FakeWeakAppClient(TestMeters.Success([meter])));

        await host.PollOnceAsync();

        var envelope = host.SinglePublished<ReadingsIngested>().Readings.Single();

        // WeakApp's `name` is the room and `type` the sensor kind; the payload stays opaque JSON
        // because flattening it into metric rows belongs to the Processor, not here.
        Assert.Equal("Corridor", envelope.Location);
        Assert.Equal("air_quality", envelope.MeterType);
        Assert.Equal("""{"co2":727,"pm25":42,"humidity":47}""", envelope.Payload);
        Assert.Equal(Sha256Hex(envelope.Payload), envelope.PayloadHash);
    }

    [Fact]
    public async Task PollOnceAsync_EmptyButWellFormedResponse_IsASuccessWithNoReadings()
    {
        await using var host = await PollingTestHost.StartAsync(new FakeWeakAppClient(TestMeters.Success([])));

        var attempt = await host.PollOnceAsync();

        // An empty array is a legitimate zero-reading success, not corruption — observed live and
        // recorded in docs/weakapp-observed-response.json.
        Assert.Equal(IngestOutcome.Success, attempt.Outcome);
        Assert.Equal(0, attempt.ReadingCount);
        Assert.Empty(host.SinglePublished<ReadingsIngested>().Readings);
    }

    [Theory]
    [InlineData(IngestOutcome.Corrupted, 200, "Error while copying content to a stream")]
    [InlineData(IngestOutcome.HttpError, 502, "WeakApp returned HTTP 502")]
    [InlineData(IngestOutcome.RateLimited, 429, "Rate limit exceeded")]
    [InlineData(IngestOutcome.Unauthorized, 401, "Invalid or missing API key")]
    [InlineData(IngestOutcome.Timeout, null, "The operation didn't complete within the allowed timeout")]
    public async Task PollOnceAsync_FailedAttempt_RecordsTheAttemptAndPublishesNoReadings(
        IngestOutcome outcome,
        int? httpStatus,
        string errorMessage)
    {
        await using var host = await PollingTestHost.StartAsync(
            new FakeWeakAppClient(TestMeters.Failure(outcome, httpStatus, errorMessage)));

        var attempt = await host.PollOnceAsync();

        var recorded = host.SinglePublished<IngestAttemptRecorded>();

        Assert.Equal(outcome, recorded.Outcome);
        Assert.Equal(httpStatus, recorded.HttpStatus);
        Assert.Equal(errorMessage, recorded.ErrorMessage);
        Assert.Equal(0, recorded.ReadingCount);
        Assert.Equal(attempt.BatchId, recorded.BatchId);
        Assert.False(await host.Harness.Published.Any<ReadingsIngested>());
    }

    [Fact]
    public async Task PollOnceAsync_UnboundedUpstreamErrorMessage_IsTruncatedBeforeItIsPublished()
    {
        var runaway = new string('x', 5000);
        await using var host = await PollingTestHost.StartAsync(
            new FakeWeakAppClient(TestMeters.Failure(IngestOutcome.HttpError, 500, runaway)));

        var attempt = await host.PollOnceAsync();

        // ingest_batches.error_message is documented as truncated (PRD §7.1); an unbounded upstream
        // body must not be copied verbatim into every message and log line.
        Assert.Equal(1024, attempt.ErrorMessage?.Length);
        Assert.StartsWith("xxxx", attempt.ErrorMessage, StringComparison.Ordinal);
    }

    private static string Sha256Hex(string payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
}
