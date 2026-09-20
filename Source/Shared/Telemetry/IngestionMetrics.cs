using System.Diagnostics.Metrics;

namespace Shared.Telemetry;

public sealed class IngestionMetrics
{
    public const string MeterName = "WeakAppHandler";

    private readonly Counter<long> _readingsIngested;
    private readonly Counter<long> _readingsStored;
    private readonly Counter<long> _duplicateBatches;
    private readonly Counter<long> _alertsRaised;

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);

        _readingsIngested = meter.CreateCounter<long>(
            "weakapphandler.readings.ingested",
            unit: "{reading}",
            description: "Readings pulled from the unstable API and published to the queue.");

        _readingsStored = meter.CreateCounter<long>(
            "weakapphandler.readings.stored",
            unit: "{reading}",
            description: "Readings committed to the database.");

        _duplicateBatches = meter.CreateCounter<long>(
            "weakapphandler.batches.duplicate",
            unit: "{batch}",
            description: "Batches the processor recognised as already handled.");

        _alertsRaised = meter.CreateCounter<long>(
            "weakapphandler.alerts.raised",
            unit: "{alert}",
            description: "Readings that left their configured threshold band.");
    }

    public void ReadingsIngested(string meterType, long count) =>
        _readingsIngested.Add(count, new KeyValuePair<string, object?>("meter_type", meterType));

    public void ReadingsStored(long count) => _readingsStored.Add(count);

    public void BatchDuplicated() => _duplicateBatches.Add(1);

    public void AlertsRaised(string kind, long count) =>
        _alertsRaised.Add(count, new KeyValuePair<string, object?>("kind", kind));
}
