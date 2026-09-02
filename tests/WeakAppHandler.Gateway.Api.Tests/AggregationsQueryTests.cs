using System.Text.Json;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-024: <c>aggregations</c> buckets readings into fixed time windows via SQL
/// <c>date_trunc</c>/<c>generate_series</c>, grouped by location/meterType/metric, with every bucket
/// in the requested range present even when no reading fell into it.
/// </summary>
/// <remarks>
/// Test step 2 ("review the generated SQL to confirm no client-side grouping") is satisfied
/// structurally rather than by a test: <see cref="Infrastructure.Persistence.GatewayReadContext.GetAggregationsAsync"/>
/// pipes <c>FromSqlInterpolated</c> directly into <c>ToListAsync</c>, so there is no LINQ
/// <c>GroupBy</c>/aggregation step for the SQL result to pass through in memory - see that method's
/// own comments for the query itself.
/// <para>
/// Metric codes come from the fixed <c>metrics</c> lookup table <c>readings.metric_code</c> has a
/// foreign key onto (<c>energy</c>/<c>co2</c>/<c>humidity</c>/<c>pm25</c>/<c>motion_detected</c>) -
/// unlike Notification's alerting schema, this one cannot take an arbitrary per-test code. Isolation
/// between concurrently-running Facts (the TASK-023 lesson: xUnit does not serialise Facts within one
/// class) therefore comes from each test's own unique location, always passed as the query's
/// <c>location</c> filter - which narrows the SQL's own <c>series</c> CTE, not merely the assertions.
/// </para>
/// </remarks>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AggregationsQueryTests(IntegrationTestFixture fixture)
{
    private const string Query = """
        query Aggregations($metricCode: String!, $bucket: AggregationBucketSize!, $from: DateTime!, $to: DateTime!, $location: String, $meterType: String) {
          aggregations(metricCode: $metricCode, bucket: $bucket, from: $from, to: $to, location: $location, meterType: $meterType) {
            bucketStart
            location
            meterType
            avg
            min
            max
            sum
            count
          }
        }
        """;

    [Fact]
    public async Task Aggregations_TwentyFourHourWindow_ReturnsExactlyTwentyFourHourlyBucketsIncludingEmptyOnes()
    {
        var location = $"agg-query-{Guid.NewGuid():N}";
        const string metricCode = "co2";
        var anchor = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var populatedHours = new HashSet<int> { 0, 5, 10, 23 };

        await using var context = await ProcessorSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);
        var meterId = await ProcessorSchemaSeed.SeedMeterAsync(context, location, "air_quality", anchor);

        // Only 4 of the 24 hours get a reading - the rest must still appear as zero-count buckets
        // rather than being silently absent (the acceptance criterion this test proves).
        foreach (var hour in populatedHours)
        {
            await ProcessorSchemaSeed.AddReadingAsync(context, meterId, metricCode, 500m, anchor.AddHours(hour));
        }

        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        var body = await GraphQlClient.PostAsync(
            client,
            Query,
            new
            {
                metricCode,
                bucket = "HOUR",
                from = anchor,
                to = anchor.AddHours(24),
                location,
            });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var buckets = body.GetProperty("data").GetProperty("aggregations");
        Assert.Equal(24, buckets.GetArrayLength());

        for (var hour = 0; hour < 24; hour++)
        {
            var element = buckets[hour];

            // Ordered by bucket_start (the query's own ORDER BY), so index i is hour i by construction.
            Assert.Equal(anchor.AddHours(hour), element.GetProperty("bucketStart").GetDateTimeOffset());
            Assert.Equal(location, element.GetProperty("location").GetString());
            Assert.Equal("air_quality", element.GetProperty("meterType").GetString());

            if (populatedHours.Contains(hour))
            {
                Assert.Equal(1, element.GetProperty("count").GetInt32());
                Assert.Equal(500m, element.GetProperty("avg").GetDecimal());
            }
            else
            {
                Assert.Equal(0, element.GetProperty("count").GetInt32());
                Assert.Equal(JsonValueKind.Null, element.GetProperty("avg").ValueKind);
                Assert.Equal(JsonValueKind.Null, element.GetProperty("min").ValueKind);
                Assert.Equal(JsonValueKind.Null, element.GetProperty("max").ValueKind);
                Assert.Equal(JsonValueKind.Null, element.GetProperty("sum").ValueKind);
            }
        }
    }

    [Fact]
    public async Task Aggregations_BucketWithKnownValues_ComputesAvgMinMaxSumMatchingManualCalculation()
    {
        var location = $"agg-query-{Guid.NewGuid():N}";
        const string metricCode = "energy";
        var bucketStart = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        await using var context = await ProcessorSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);
        var meterId = await ProcessorSchemaSeed.SeedMeterAsync(context, location, "energy", bucketStart);

        // 10, 20, 30 inside the same hour: avg 20, min 10, max 30, sum 60, count 3 - a result a
        // reviewer can check by hand, which is exactly what this acceptance criterion asks for.
        await ProcessorSchemaSeed.AddReadingAsync(context, meterId, metricCode, 10m, bucketStart.AddMinutes(5));
        await ProcessorSchemaSeed.AddReadingAsync(context, meterId, metricCode, 20m, bucketStart.AddMinutes(25));
        await ProcessorSchemaSeed.AddReadingAsync(context, meterId, metricCode, 30m, bucketStart.AddMinutes(45));

        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        var body = await GraphQlClient.PostAsync(
            client,
            Query,
            new
            {
                metricCode,
                bucket = "HOUR",
                from = bucketStart,
                to = bucketStart.AddHours(1),
                location,
            });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var singleBucket = Assert.Single(body.GetProperty("data").GetProperty("aggregations").EnumerateArray());

        Assert.Equal(3, singleBucket.GetProperty("count").GetInt32());
        Assert.Equal(20m, singleBucket.GetProperty("avg").GetDecimal());
        Assert.Equal(10m, singleBucket.GetProperty("min").GetDecimal());
        Assert.Equal(30m, singleBucket.GetProperty("max").GetDecimal());
        Assert.Equal(60m, singleBucket.GetProperty("sum").GetDecimal());
    }

    [Fact]
    public async Task Aggregations_FilteredByLocation_ExcludesAnotherLocationsSeriesAndReadings()
    {
        var targetLocation = $"agg-query-target-{Guid.NewGuid():N}";
        var otherLocation = $"agg-query-other-{Guid.NewGuid():N}";
        const string metricCode = "energy";
        var bucketStart = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        await using var context = await ProcessorSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);
        var targetMeterId = await ProcessorSchemaSeed.SeedMeterAsync(context, targetLocation, "energy", bucketStart);
        var otherMeterId = await ProcessorSchemaSeed.SeedMeterAsync(context, otherLocation, "energy", bucketStart);

        await ProcessorSchemaSeed.AddReadingAsync(context, targetMeterId, metricCode, 100m, bucketStart.AddMinutes(10));

        // A different location's own reading, in the same window - must not leak into the target
        // location's bucket, and the other location's own (location, meterType) pair must not add a
        // second row to the result at all once the location filter narrows the `series` CTE.
        await ProcessorSchemaSeed.AddReadingAsync(context, otherMeterId, metricCode, 999m, bucketStart.AddMinutes(10));

        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        var body = await GraphQlClient.PostAsync(
            client,
            Query,
            new
            {
                metricCode,
                bucket = "HOUR",
                from = bucketStart,
                to = bucketStart.AddHours(1),
                location = targetLocation,
            });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var singleBucket = Assert.Single(body.GetProperty("data").GetProperty("aggregations").EnumerateArray());

        Assert.Equal(targetLocation, singleBucket.GetProperty("location").GetString());
        Assert.Equal(1, singleBucket.GetProperty("count").GetInt32());
        Assert.Equal(100m, singleBucket.GetProperty("avg").GetDecimal());
    }
}
