using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Moq;
using NotificationService.BLL.Models;
using NotificationService.BLL.Notifications;
using NotificationService.BLL.Services;
using Shared.Configuration;
using Xunit;

namespace NotificationService.Tests;

public class NotificationDispatchServiceTests
{
    [Fact]
    public void Dispatch_FansOutTo_All_Location_Metric_AndBothCombined()
    {
        var dispatch = Create().Dispatch([
            Reading("Kitchen", "co2", 500),
            Reading("Office", "humidity", 50),
        ]);

        dispatch.Envelopes.Select(envelope => envelope.Group).Should().BeEquivalentTo(new[]
        {
            NotificationGroups.All,
            NotificationGroups.ForLocation("Kitchen"),
            NotificationGroups.ForLocation("Office"),
            NotificationGroups.ForMetric("co2"),
            NotificationGroups.ForMetric("humidity"),
            NotificationGroups.ForLocationAndMetric("Kitchen", "co2"),
            NotificationGroups.ForLocationAndMetric("Office", "humidity"),
        });
    }

    [Fact]
    public void Dispatch_DropsGroupsThatWouldCarryNothing()
    {
        var dispatch = Create().Dispatch([
            Reading("Kitchen", "co2", 500),
            Reading("Office", "humidity", 50),
        ]);

        dispatch.Envelopes.Should().NotContain(envelope =>
            envelope.Group == NotificationGroups.ForLocationAndMetric("Kitchen", "humidity"));
        dispatch.Envelopes.Should().OnlyContain(envelope => envelope.Readings.Count > 0);
    }

    [Fact]
    public void Dispatch_GivesEachGroupOnlyItsOwnReadings()
    {
        var dispatch = Create().Dispatch([
            Reading("Kitchen", "co2", 500),
            Reading("Office", "co2", 600),
        ]);

        var kitchen = dispatch.Envelopes
            .Single(envelope => envelope.Group == NotificationGroups.ForLocation("Kitchen"));

        kitchen.Readings.Should().ContainSingle().Which.Location.Should().Be("Kitchen");

        dispatch.Envelopes
            .Single(envelope => envelope.Group == NotificationGroups.All)
            .Readings.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(1200, AlertKind.Above, 1000)]
    [InlineData(999, null, 0)]
    public void Dispatch_RaisesAnAlert_OnlyWhenTheValueLeavesTheBand(
        decimal value,
        AlertKind? expected,
        decimal threshold)
    {
        var dispatch = Create().Dispatch([Reading("Kitchen", "co2", value)]);

        if (expected is null)
        {
            dispatch.AlertCount.Should().Be(0);
            return;
        }

        dispatch.AlertCount.Should().Be(1);

        var alert = dispatch.Envelopes
            .Single(envelope => envelope.Group == NotificationGroups.All)
            .Alerts.Should().ContainSingle().Subject;

        alert.Kind.Should().Be(expected);
        alert.Threshold.Should().Be(threshold);
    }

    [Fact]
    public void Dispatch_RaisesBelow_WhenTheValueDropsUnderTheFloor()
    {
        var dispatch = Create().Dispatch([Reading("Office", "humidity", 12)]);

        var alert = dispatch.Envelopes
            .Single(envelope => envelope.Group == NotificationGroups.All)
            .Alerts.Should().ContainSingle().Subject;

        alert.Kind.Should().Be(AlertKind.Below);
        alert.Threshold.Should().Be(30);
    }

    [Fact]
    public void Dispatch_IgnoresMetricsWithoutAConfiguredBand()
    {
        var dispatch = Create().Dispatch([Reading("Garage", "pressure", 99_999)]);

        dispatch.AlertCount.Should().Be(0);
        dispatch.ReadingCount.Should().Be(1);
    }

    [Fact]
    public void Dispatch_IgnoresBooleanReadings_BecauseTheyHaveNothingToCompare()
    {
        var reading = new ReadingNotificationModel(
            1, Guid.NewGuid(), "Bedroom", "motion", "motion_detected",
            DateTimeOffset.UtcNow, Numeric: null, Flag: true);

        Create().Dispatch([reading]).AlertCount.Should().Be(0);
    }

    [Fact]
    public void Dispatch_CarriesTheAlert_IntoEveryGroupThatSeesItsReading()
    {
        var dispatch = Create().Dispatch([Reading("Kitchen", "co2", 1200)]);

        dispatch.Envelopes.Should().OnlyContain(envelope => envelope.Alerts.Count == 1);
    }

    private static NotificationDispatchService Create()
    {
        var options = new ThresholdOptions
        {
            Metrics =
            [
                new MetricThresholdOptions { MetricCode = "co2", Unit = "ppm", Max = 1000 },
                new MetricThresholdOptions { MetricCode = "humidity", Unit = "%", Min = 30, Max = 70 },
            ],
        };

        var monitor = new Mock<IOptionsMonitor<ThresholdOptions>>();
        monitor.SetupGet(instance => instance.CurrentValue).Returns(options);

        return new NotificationDispatchService(monitor.Object);
    }

    private static ReadingNotificationModel Reading(string location, string metricCode, decimal value) =>
        new(
            Id: Random.Shared.NextInt64(1, long.MaxValue),
            MeterId: Guid.NewGuid(),
            Location: location,
            MeterType: "air_quality",
            MetricCode: metricCode,
            ObservedAt: DateTimeOffset.UtcNow,
            Numeric: value,
            Flag: null);
}
