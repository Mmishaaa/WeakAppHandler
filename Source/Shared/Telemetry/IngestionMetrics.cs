using System.Diagnostics.Metrics;

namespace Shared.Telemetry;

public sealed class IngestionMetrics
{
    public const string MeterName = "WeakAppHandler";

    private readonly Counter<long> _readingsIngested;
    private readonly Counter<long> _batchesPublishFailed;
    private readonly Counter<long> _readingsStored;
    private readonly Counter<long> _alertsRaised;

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);

        _readingsIngested = meter.CreateCounter<long>(
            "weakapphandler.readings.ingested",
            unit: "{reading}",
            description: "Readings pulled from the unstable API and published to the queue.");

        _batchesPublishFailed = meter.CreateCounter<long>(
            "weakapphandler.batches.publish_failed",
            unit: "{batch}",
            description: "Batches the ingestor could not publish to the queue and therefore dropped.");

        _readingsStored = meter.CreateCounter<long>(
            "weakapphandler.readings.stored",
            unit: "{reading}",
            description: "Readings committed to the database.");

        _alertsRaised = meter.CreateCounter<long>(
            "weakapphandler.alerts.raised",
            unit: "{alert}",
            description: "Readings that left their configured threshold band.");
    }

    public void ReadingsIngested(string meterType, long count) =>
        _readingsIngested.Add(count, new KeyValuePair<string, object?>("meter_type", meterType));

    public void BatchPublishFailed() => _batchesPublishFailed.Add(1);

    public void ReadingsStored(long count) => _readingsStored.Add(count);

    public void AlertsRaised(string kind, long count) =>
        _alertsRaised.Add(count, new KeyValuePair<string, object?>("kind", kind));
}
