using AwesomeAssertions;
using GraphQlGateway.BLL.Models;
using GraphQlGateway.BLL.Services;
using GraphQlGateway.DAL.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Configuration;
using Shared.Entities;
using Xunit;

namespace GraphQlGateway.Tests;

public class ReadingStatsServiceTests
{
    private readonly Mock<IReadingRepository> _repository = new(MockBehavior.Strict);

    [Fact]
    public async Task GetTimeBucketsAsync_PassesTheRequestedBucket_ToTheDatabase()
    {
        _repository
            .Setup(repository => repository.GetBucketAggregatesAsync(
                "co2",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                ReadingBucket.Day,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Service().GetTimeBucketsAsync(Input(TimeBucket.Day), CancellationToken.None);

        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetTimeBucketsAsync_BuildsBucketStarts_FromTheDatePartsTheDatabaseReturned()
    {
        _repository
            .Setup(repository => repository.GetBucketAggregatesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<ReadingBucket>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([Bucket(string.Empty, 2026, 9, 20, 14, count: 6, average: 700m)]);

        var buckets = await Service().GetTimeBucketsAsync(Input(TimeBucket.Hour), CancellationToken.None);

        var bucket = buckets.Should().ContainSingle().Subject;

        bucket.BucketStart.Should().Be(new DateTimeOffset(2026, 9, 20, 14, 0, 0, TimeSpan.Zero));
        bucket.Count.Should().Be(6);
        bucket.Average.Should().Be(700m);
    }

    [Fact]
    public async Task GetLocationSeriesAsync_SplitsOneFlatResult_IntoASeriesPerLocation()
    {
        _repository
            .Setup(repository => repository.GetLocationBucketAggregatesAsync(
                "co2",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                ReadingBucket.Hour,
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Bucket("Kitchen", 2026, 9, 20, 10, average: 500m),
                Bucket("Kitchen", 2026, 9, 20, 11, average: 520m),
                Bucket("Office", 2026, 9, 20, 10, average: 610m),
            ]);

        var series = await Service().GetLocationSeriesAsync(
            Input(TimeBucket.Hour), locations: null, CancellationToken.None);

        series.Select(entry => entry.Location).Should().Equal("Kitchen", "Office");
        series.Select(entry => entry.Buckets.Count).Should().Equal(2, 1);
    }

    [Fact]
    public async Task GetMetricSnapshotAsync_PicksTheHighestReading_PerMetric()
    {
        SetupLatest(
            Reading("Kitchen", "air_quality", "co2", 500m),
            Reading("Office", "air_quality", "co2", 940m),
            Reading("Garage", "air_quality", "co2", 700m));

        var snapshot = await Service().GetMetricSnapshotAsync(CancellationToken.None);

        var metric = snapshot.Should().ContainSingle().Subject;

        metric.Location.Should().Be("Office");
        metric.Numeric.Should().Be(940m);
        metric.LocationCount.Should().Be(3);
        metric.Unit.Should().Be("ppm");
    }

    [Theory]
    [InlineData(1200, MetricState.Above, 1000)]
    [InlineData(900, MetricState.Ok, 1000)]
    public async Task GetMetricSnapshotAsync_ComparesAgainstTheConfiguredBand(
        decimal value,
        MetricState expected,
        decimal threshold)
    {
        SetupLatest(Reading("Kitchen", "air_quality", "co2", value));

        var metric = (await Service().GetMetricSnapshotAsync(CancellationToken.None))
            .Should().ContainSingle().Subject;

        metric.State.Should().Be(expected);
        metric.Threshold.Should().Be(threshold);
    }

    [Fact]
    public async Task GetMetricSnapshotAsync_ReportsBelow_WhenTheValueDropsUnderTheFloor()
    {
        SetupLatest(Reading("Office", "air_quality", "humidity", 11m));

        var metric = (await Service().GetMetricSnapshotAsync(CancellationToken.None))
            .Should().ContainSingle().Subject;

        metric.State.Should().Be(MetricState.Below);
        metric.Threshold.Should().Be(30m);
    }

    [Fact]
    public async Task GetMetricSnapshotAsync_ReportsUnknown_ForMetricsWithoutABand()
    {
        SetupLatest(Reading("Garage", "air_quality", "pressure", 1013m));

        var metric = (await Service().GetMetricSnapshotAsync(CancellationToken.None))
            .Should().ContainSingle().Subject;

        metric.State.Should().Be(MetricState.Unknown);
        metric.Threshold.Should().BeNull();
        metric.Unit.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMetricSnapshotAsync_ReportsUnknown_ForBooleanMetrics()
    {
        var reading = Reading("Bedroom", "motion", "motion_detected", numeric: null);
        reading.ValueBool = true;

        SetupLatest(reading);

        var metric = (await Service().GetMetricSnapshotAsync(CancellationToken.None))
            .Should().ContainSingle().Subject;

        metric.State.Should().Be(MetricState.Unknown);
        metric.Flag.Should().BeTrue();
    }

    [Fact]
    public async Task GetMetricSnapshotAsync_OrdersMetricsByCode()
    {
        SetupLatest(
            Reading("Kitchen", "air_quality", "pm25", 20m),
            Reading("Kitchen", "air_quality", "co2", 500m),
            Reading("Kitchen", "air_quality", "humidity", 50m));

        var snapshot = await Service().GetMetricSnapshotAsync(CancellationToken.None);

        snapshot.Select(metric => metric.MetricCode).Should().Equal("co2", "humidity", "pm25");
    }

    private static ReadingStatsModel Input(TimeBucket bucket) =>
        new("co2", bucket, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), null, null);

    private static ReadingBucketAggregate Bucket(
        string location,
        int year,
        int month,
        int day,
        int hour,
        int count = 1,
        decimal? average = null) =>
        new(location, year, month, day, hour, count, 0, 0m, average, average, average);

    private static DbReading Reading(
        string location,
        string meterType,
        string metricCode,
        decimal? numeric) =>
        new()
        {
            Id = Random.Shared.NextInt64(1, long.MaxValue),
            MeterId = Guid.NewGuid(),
            Meter = new DbMeter { Id = Guid.NewGuid(), Location = location, MeterType = meterType },
            MetricCode = metricCode,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ValueNumeric = numeric,
        };

    private void SetupLatest(params DbReading[] readings) =>
        _repository
            .Setup(repository => repository.GetLatestAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readings);

    private ReadingStatsService Service()
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

        return new ReadingStatsService(_repository.Object, monitor.Object);
    }
}
