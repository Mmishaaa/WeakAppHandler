using System.Diagnostics.Metrics;
using Polly.CircuitBreaker;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Ingestor.Telemetry;

/// <summary>
/// The Ingestor's domain metrics (TASK-044, PRD §6 F10): poll outcome/latency and the resilience
/// pipeline's circuit-breaker state. A singleton so the <see cref="Meter"/> and its instruments are
/// created once for the process and read the same live <see cref="CircuitBreakerStateProvider"/> the
/// admin status endpoint already reports from (TASK-017) rather than a second, driftable copy.
/// </summary>
public sealed class IngestorMetrics : IDisposable
{
    public const string MeterName = "WeakAppHandler.Ingestor";

    private readonly Meter _meter;
    private readonly Counter<long> _pollOutcomes;
    private readonly Histogram<double> _pollDuration;

    public IngestorMetrics(CircuitBreakerStateProvider circuitBreakerState)
    {
        ArgumentNullException.ThrowIfNull(circuitBreakerState);

        _meter = new Meter(MeterName);
        _pollOutcomes = _meter.CreateCounter<long>(
            "ingestor.poll.outcomes",
            unit: "{poll}",
            description: "Count of WeakApp poll attempts, tagged by outcome.");
        _pollDuration = _meter.CreateHistogram<double>(
            "ingestor.poll.duration",
            unit: "ms",
            description: "Duration of each WeakApp poll attempt, tagged by outcome.");

        // Observed at collection time rather than pushed on state-change: Polly's provider already
        // holds the current state live, so a gauge callback is the current value by construction and
        // cannot drift the way a mirrored counter updated from a state-change event could.
        _meter.CreateObservableGauge(
            "ingestor.circuit_breaker.state",
            () => (long)MapState(circuitBreakerState.CircuitState),
            unit: "{state}",
            description: "Resilience circuit breaker state: 0=Closed, 1=HalfOpen, 2=Open, 3=Isolated.");
    }

    /// <summary>
    /// Exposed so tests can attach a <see cref="System.Diagnostics.Metrics.MeterListener"/> scoped to
    /// this exact instance rather than by meter name - multiple <see cref="IngestorMetrics"/>
    /// instances share the same name across parallel test hosts, and a listener filtering by name
    /// alone would pick up another test's measurements too.
    /// </summary>
    public Meter Meter => _meter;

    public void RecordPoll(IngestOutcome outcome, double durationMs)
    {
        var outcomeTag = new KeyValuePair<string, object?>("outcome", outcome.ToString());
        _pollOutcomes.Add(1, outcomeTag);
        _pollDuration.Record(durationMs, outcomeTag);
    }

    public void Dispose() => _meter.Dispose();

    private static int MapState(CircuitState state) => state switch
    {
        CircuitState.Closed => 0,
        CircuitState.HalfOpen => 1,
        CircuitState.Open => 2,
        CircuitState.Isolated => 3,
        _ => -1,
    };
}
