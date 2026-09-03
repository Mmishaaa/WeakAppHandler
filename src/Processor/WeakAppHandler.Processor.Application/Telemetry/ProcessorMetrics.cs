using System.Diagnostics.Metrics;
using WeakAppHandler.Processor.Application.Ingestion;

namespace WeakAppHandler.Processor.Application.Telemetry;

/// <summary>
/// The Processor's domain metrics (TASK-044, PRD §6 F10): processing latency, dedup count and
/// dead-lettered count. A separate singleton from <see cref="Stats.ProcessingStatsState"/> rather than
/// folded into it: that state exists for the in-memory admin snapshot (TASK-021), this exists for
/// Prometheus export, and the two consumers of a result already call both without either depending on
/// the other.
/// </summary>
public sealed class ProcessorMetrics : IDisposable
{
    public const string MeterName = "WeakAppHandler.Processor";

    private readonly Meter _meter;
    private readonly Counter<long> _recorded;
    private readonly Counter<long> _deduplicated;
    private readonly Counter<long> _deadLettered;
    private readonly Histogram<double> _processingDuration;

    public ProcessorMetrics()
    {
        _meter = new Meter(MeterName);
        _recorded = _meter.CreateCounter<long>(
            "processor.messages.recorded",
            unit: "{message}",
            description: "Ingestion messages newly recorded into ingest_batches/readings.");
        _deduplicated = _meter.CreateCounter<long>(
            "processor.messages.deduplicated",
            unit: "{message}",
            description: "Redelivered ingestion messages discarded as duplicates of an already-recorded one.");
        _deadLettered = _meter.CreateCounter<long>(
            "processor.messages.dead_lettered",
            unit: "{message}",
            description: "Messages that exhausted their receive endpoint's retry policy.");
        _processingDuration = _meter.CreateHistogram<double>(
            "processor.processing.duration",
            unit: "ms",
            description: "Time to record one consumed ingestion message, tagged by message type.");
    }

    /// <summary>
    /// Exposed so tests can attach a <see cref="System.Diagnostics.Metrics.MeterListener"/> scoped to
    /// this exact instance rather than by meter name - multiple <see cref="ProcessorMetrics"/>
    /// instances share the same name across parallel test hosts, and a listener filtering by name
    /// alone would pick up another test's measurements too.
    /// </summary>
    public Meter Meter => _meter;

    public void RecordResult(IngestionRecordResult result)
    {
        switch (result)
        {
            case IngestionRecordResult.Recorded:
                _recorded.Add(1);
                break;
            case IngestionRecordResult.Duplicate:
                _deduplicated.Add(1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown ingestion record result.");
        }
    }

    public void RecordDeadLettered() => _deadLettered.Add(1);

    public void RecordProcessingDuration(string messageType, double durationMs) =>
        _processingDuration.Record(durationMs, new KeyValuePair<string, object?>("message_type", messageType));

    public void Dispose() => _meter.Dispose();
}
