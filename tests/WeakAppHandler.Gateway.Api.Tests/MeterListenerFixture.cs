using System.Diagnostics.Metrics;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// Captures every measurement recorded on instruments belonging to one specific <see cref="Meter"/>
/// instance, via a raw <see cref="MeterListener"/> rather than the full OTel SDK - the SDK's wildcard
/// AddMeter wiring is proven separately in WeakAppHandler.ServiceDefaults.Tests. Filters by reference
/// rather than by name: every metrics class in this codebase reuses the same meter name across every
/// instance (including one per parallel test host), so a listener filtering by name alone would also
/// pick up unrelated measurements from whatever other test happens to be running concurrently.
/// </summary>
internal sealed class MeterListenerFixture : IDisposable
{
    private readonly MeterListener _listener;

    public MeterListenerFixture(Meter meter)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            DoubleMeasurements.Add((instrument.Name, value, tags.ToArray())));

        _listener.Start();
    }

    public List<(string Instrument, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> DoubleMeasurements { get; } = [];

    public void Dispose() => _listener.Dispose();
}
