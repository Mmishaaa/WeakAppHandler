using System.Text.Json;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Contracts.Tests;

public class JsonRoundTripTests
{
    [Fact]
    public void IngestAttemptRecorded_RoundTripsThroughJson()
    {
        var original = new IngestAttemptRecorded(
            MessageId: Guid.NewGuid(),
            BatchId: Guid.NewGuid(),
            FetchedAt: DateTimeOffset.UtcNow,
            Outcome: IngestOutcome.HttpError,
            HttpStatus: 502,
            DurationMs: 1234,
            ReadingCount: 0,
            ErrorMessage: "Bad Gateway");

        var roundTripped = RoundTrip(original);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void ReadingsIngested_RoundTripsThroughJson()
    {
        var original = new ReadingsIngested(
            MessageId: Guid.NewGuid(),
            BatchId: Guid.NewGuid(),
            FetchedAt: DateTimeOffset.UtcNow,
            SourceLatencyMs: 87,
            Readings:
            [
                new MeterReadingEnvelope("Kitchen", "energy", "{\"energyUsage\":5.2}", "abc123"),
                new MeterReadingEnvelope("Garage", "motion", "{\"motionDetected\":true}", "def456"),
            ]);

        var roundTripped = RoundTrip(original);

        Assert.Equal(original.MessageId, roundTripped.MessageId);
        Assert.Equal(original.BatchId, roundTripped.BatchId);
        Assert.Equal(original.FetchedAt, roundTripped.FetchedAt);
        Assert.Equal(original.SourceLatencyMs, roundTripped.SourceLatencyMs);
        Assert.Equal(original.Readings, roundTripped.Readings);
    }

    [Fact]
    public void ReadingStored_RoundTripsThroughJson()
    {
        var original = new ReadingStored(
            MeterId: Guid.NewGuid(),
            Location: "Bedroom",
            MeterType: "air_quality",
            MetricCode: "co2",
            Value: new MetricValue(Numeric: 950.5, Boolean: null),
            PreviousValue: new MetricValue(Numeric: 900.0, Boolean: null),
            IsChanged: true,
            ObservedAt: DateTimeOffset.UtcNow);

        var roundTripped = RoundTrip(original);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void ReadingStored_WithNullPreviousValue_RoundTripsThroughJson()
    {
        var original = new ReadingStored(
            MeterId: Guid.NewGuid(),
            Location: "Garage",
            MeterType: "motion",
            MetricCode: "motion_detected",
            Value: new MetricValue(Numeric: null, Boolean: true),
            PreviousValue: null,
            IsChanged: true,
            ObservedAt: DateTimeOffset.UtcNow);

        var roundTripped = RoundTrip(original);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void AlertRaised_RoundTripsThroughJson()
    {
        var original = new AlertRaised(
            AlertId: Guid.NewGuid(),
            RuleId: Guid.NewGuid(),
            MeterId: Guid.NewGuid(),
            Location: "Garage",
            MeterType: "motion",
            MetricCode: "motion_detected",
            Severity: "warning",
            TriggeredValue: new MetricValue(Numeric: null, Boolean: true),
            TriggeredAt: DateTimeOffset.UtcNow);

        var roundTripped = RoundTrip(original);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void AlertResolved_RoundTripsThroughJson()
    {
        var original = new AlertResolved(
            AlertId: Guid.NewGuid(),
            RuleId: Guid.NewGuid(),
            MeterId: Guid.NewGuid(),
            Location: "Bedroom",
            MeterType: "air_quality",
            MetricCode: "co2",
            Severity: "critical",
            ResolvedValue: new MetricValue(Numeric: 800.0, Boolean: null),
            ResolvedAt: DateTimeOffset.UtcNow);

        var roundTripped = RoundTrip(original);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void IngestAttemptRecorded_And_ReadingsIngested_FromSameAttempt_ShareBatchId()
    {
        var batchId = Guid.NewGuid();
        var fetchedAt = DateTimeOffset.UtcNow;

        var attempt = new IngestAttemptRecorded(
            MessageId: Guid.NewGuid(),
            BatchId: batchId,
            FetchedAt: fetchedAt,
            Outcome: IngestOutcome.Success,
            HttpStatus: 200,
            DurationMs: 42,
            ReadingCount: 1,
            ErrorMessage: null);

        var ingested = new ReadingsIngested(
            MessageId: Guid.NewGuid(),
            BatchId: batchId,
            FetchedAt: fetchedAt,
            SourceLatencyMs: 10,
            Readings: [new MeterReadingEnvelope("Kitchen", "energy", "{}", "hash")]);

        Assert.Equal(attempt.BatchId, ingested.BatchId);
        Assert.NotEqual(attempt.MessageId, ingested.MessageId);
    }

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Deserialization returned null.");
    }
}
