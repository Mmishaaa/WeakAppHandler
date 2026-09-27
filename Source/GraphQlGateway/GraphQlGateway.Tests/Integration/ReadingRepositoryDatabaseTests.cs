using AwesomeAssertions;
using GraphQlGateway.DAL.Models;
using GraphQlGateway.DAL.Repositories;
using Xunit;

namespace GraphQlGateway.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class ReadingRepositoryDatabaseTests(PostgresFixture postgres)
{
    private DateTimeOffset From => postgres.Origin;

    private DateTimeOffset To => postgres.Origin.AddDays(1);

    [Fact]
    public async Task GetBucketAggregatesAsync_GroupsByHour_InTheDatabase()
    {
        var buckets = await Repository().GetBucketAggregatesAsync(
            "co2", From, To, ReadingBucket.Hour, null, "Kitchen", CancellationToken.None);

        buckets.Should().HaveCount(2);
        buckets.Select(bucket => bucket.Hour).Should().Equal(0, 1);
        buckets[0].Count.Should().Be(2);
        buckets[0].Average.Should().Be(500m);
        buckets[1].Average.Should().Be(800m);
    }

    [Fact]
    public async Task GetBucketAggregatesAsync_CollapsesAWholeDay_WhenAskedForDayBuckets()
    {
        var buckets = await Repository().GetBucketAggregatesAsync(
            "co2", From, To, ReadingBucket.Day, null, "Kitchen", CancellationToken.None);

        var bucket = buckets.Should().ContainSingle().Subject;

        bucket.Count.Should().Be(3);
        bucket.Min.Should().Be(400m);
        bucket.Max.Should().Be(800m);
    }

    [Fact]
    public async Task GetLocationBucketAggregatesAsync_ReturnsOneRowPerLocationAndBucket()
    {
        var aggregates = await Repository().GetLocationBucketAggregatesAsync(
            "co2", From, To, ReadingBucket.Hour, [], CancellationToken.None);

        aggregates.Select(aggregate => (aggregate.Location, aggregate.Hour))
            .Should().Equal(("Kitchen", 0), ("Kitchen", 1), ("Office", 0), ("Office", 1));
    }

    [Fact]
    public async Task GetLocationBucketAggregatesAsync_NarrowsToTheRequestedLocations()
    {
        var aggregates = await Repository().GetLocationBucketAggregatesAsync(
            "co2", From, To, ReadingBucket.Hour, ["Office"], CancellationToken.None);

        aggregates.Should().OnlyContain(aggregate => aggregate.Location == "Office");
    }

    [Fact]
    public async Task GetLocationAggregatesAsync_OrdersByAverageDescending()
    {
        var aggregates = await Repository().GetLocationAggregatesAsync(
            From, To, "co2", CancellationToken.None);

        aggregates.Select(aggregate => aggregate.Location).Should().Equal("Office", "Kitchen");
        aggregates[0].Average.Should().Be(1000m);
    }

    [Fact]
    public async Task GetMeterTypeAggregatesAsync_GroupsEveryMetric_ByTheKindOfMeter()
    {
        var aggregates = await Repository().GetMeterTypeAggregatesAsync(
            From, To, null, CancellationToken.None);

        aggregates.Select(aggregate => (aggregate.MeterType, aggregate.MetricCode))
            .Should().Equal(("air_quality", "co2"), ("motion", "motion_detected"));
    }

    [Fact]
    public async Task Aggregates_CountBooleanReadings_WithoutAveragingThem()
    {
        var aggregates = await Repository().GetMeterTypeAggregatesAsync(
            From, To, "motion_detected", CancellationToken.None);

        var motion = aggregates.Should().ContainSingle().Subject;

        motion.Count.Should().Be(3);
        motion.TrueCount.Should().Be(2);
        motion.TrueShare.Should().BeApproximately(2m / 3m, 0.0001m);
        motion.Average.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsTheNewestReading_PerMeterAndMetric()
    {
        var readings = await Repository().GetLatestAsync(null, null, CancellationToken.None);

        readings.Should().HaveCount(3);
        readings.Select(reading => reading.Meter.Location)
            .Should().Equal("Bedroom", "Kitchen", "Office");
        readings.Single(reading => reading.Meter.Location == "Office")
            .ValueNumeric.Should().Be(1100m);
    }

    [Fact]
    public async Task GetLatestAsync_FiltersByMeterType()
    {
        var readings = await Repository().GetLatestAsync(null, "motion", CancellationToken.None);

        readings.Should().ContainSingle()
            .Which.MetricCode.Should().Be("motion_detected");
    }

    private ReadingRepository Repository() => new(postgres.DbContextFactory);
}
