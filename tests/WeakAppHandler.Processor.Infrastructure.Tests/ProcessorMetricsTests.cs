using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Application.Telemetry;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-044's Processor metrics in isolation, no broker or database involved: <see cref="ProcessorMetrics"/>
/// only wraps a <see cref="System.Diagnostics.Metrics.Meter"/>, so its own correctness is a pure unit
/// test. That the real consumers actually call it end to end is
/// <see cref="IngestionConsumerTests"/>'s job.
/// </summary>
public sealed class ProcessorMetricsTests
{
    [Fact]
    public void RecordResult_Recorded_IncrementsRecordedCounterOnly()
    {
        using var metrics = new ProcessorMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordResult(IngestionRecordResult.Recorded);

        Assert.Equal(1, listener.LongMeasurements.Single(m => m.Instrument == "processor.messages.recorded").Value);
        Assert.DoesNotContain(listener.LongMeasurements, m => m.Instrument == "processor.messages.deduplicated");
    }

    [Fact]
    public void RecordResult_Duplicate_IncrementsDeduplicatedCounterOnly()
    {
        using var metrics = new ProcessorMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordResult(IngestionRecordResult.Duplicate);

        Assert.Equal(1, listener.LongMeasurements.Single(m => m.Instrument == "processor.messages.deduplicated").Value);
        Assert.DoesNotContain(listener.LongMeasurements, m => m.Instrument == "processor.messages.recorded");
    }

    [Fact]
    public void RecordDeadLettered_IncrementsDeadLetteredCounter()
    {
        using var metrics = new ProcessorMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordDeadLettered();
        metrics.RecordDeadLettered();

        Assert.Equal(2, listener.LongMeasurements.Count(m => m.Instrument == "processor.messages.dead_lettered"));
    }

    [Fact]
    public void RecordProcessingDuration_TagsTheMeasurementWithMessageType()
    {
        using var metrics = new ProcessorMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordProcessingDuration("readings_ingested", 8.5);

        var measurement = listener.DoubleMeasurements.Single(m => m.Instrument == "processor.processing.duration");
        Assert.Equal(8.5, measurement.Value);
        Assert.Equal("readings_ingested", measurement.Tags.Single(t => t.Key == "message_type").Value);
    }
}
