using WeakAppHandler.Notification.Api.Telemetry;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// TASK-044's Notification metrics in isolation, no broker or database involved: <see cref="NotificationMetrics"/>
/// only wraps a <see cref="System.Diagnostics.Metrics.Meter"/>, so its own correctness is a pure unit
/// test. That the real consumer actually calls it end to end is <see cref="AlertingConsumerTests"/>'s
/// job.
/// </summary>
public sealed class NotificationMetricsTests
{
    [Fact]
    public void RecordRaised_TagsTheMeasurementWithSeverity()
    {
        using var metrics = new NotificationMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordRaised("critical");

        var measurement = listener.LongMeasurements.Single(m => m.Instrument == "notification.alerts.raised");
        Assert.Equal(1, measurement.Value);
        Assert.Equal("critical", measurement.Tags.Single(t => t.Key == "severity").Value);
    }

    [Fact]
    public void RecordResolved_TagsTheMeasurementWithSeverity()
    {
        using var metrics = new NotificationMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordResolved("warning");

        var measurement = listener.LongMeasurements.Single(m => m.Instrument == "notification.alerts.resolved");
        Assert.Equal(1, measurement.Value);
        Assert.Equal("warning", measurement.Tags.Single(t => t.Key == "severity").Value);
    }

    [Fact]
    public void RecordRaisedAndResolved_AreIndependentCounters()
    {
        using var metrics = new NotificationMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordRaised("warning");

        Assert.DoesNotContain(listener.LongMeasurements, m => m.Instrument == "notification.alerts.resolved");
    }
}
