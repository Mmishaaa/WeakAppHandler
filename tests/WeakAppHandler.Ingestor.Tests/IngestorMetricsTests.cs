using System.Diagnostics.Metrics;
using Polly.CircuitBreaker;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.Telemetry;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// TASK-044's Ingestor metrics in isolation, no broker involved: <see cref="IngestorMetrics"/> only
/// wraps a <see cref="Meter"/>, so its own correctness is a pure unit test. The real poll's tags are
/// covered by <see cref="IngestionPollerTracingTests"/> and the end-to-end scenarios.
/// </summary>
public sealed class IngestorMetricsTests
{
    [Fact]
    public void RecordPoll_Success_IncrementsOutcomeCounterAndRecordsDuration()
    {
        using var metrics = new IngestorMetrics(new CircuitBreakerStateProvider());
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordPoll(IngestOutcome.Success, 12.5);

        var count = listener.LongMeasurements.Single(m => m.Instrument == "ingestor.poll.outcomes");
        var duration = listener.DoubleMeasurements.Single(m => m.Instrument == "ingestor.poll.duration");

        Assert.Equal(1, count.Value);
        Assert.Equal("Success", GetTag(count.Tags, "outcome"));
        Assert.Equal(12.5, duration.Value);
        Assert.Equal("Success", GetTag(duration.Tags, "outcome"));
    }

    [Fact]
    public void RecordPoll_MultipleOutcomes_TagsEachMeasurementWithItsOwnOutcome()
    {
        using var metrics = new IngestorMetrics(new CircuitBreakerStateProvider());
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordPoll(IngestOutcome.HttpError, 5);
        metrics.RecordPoll(IngestOutcome.RateLimited, 6);

        var outcomes = listener.LongMeasurements
            .Where(m => m.Instrument == "ingestor.poll.outcomes")
            .Select(m => GetTag(m.Tags, "outcome"))
            .ToList();

        Assert.Contains("HttpError", outcomes);
        Assert.Contains("RateLimited", outcomes);
    }

    [Fact]
    public void CircuitBreakerStateGauge_FreshProvider_ReportsClosed()
    {
        using var metrics = new IngestorMetrics(new CircuitBreakerStateProvider());
        using var listener = new MeterListenerFixture(metrics.Meter);

        listener.CollectObservableInstruments();

        var gauge = listener.LongMeasurements.Single(m => m.Instrument == "ingestor.circuit_breaker.state");
        Assert.Equal(0, gauge.Value);
    }

    private static string? GetTag(IReadOnlyList<KeyValuePair<string, object?>> tags, string key) =>
        tags.SingleOrDefault(t => t.Key == key).Value?.ToString();
}
