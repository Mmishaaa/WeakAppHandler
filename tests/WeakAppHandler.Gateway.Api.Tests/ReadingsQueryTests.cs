using System.Text.Json;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-023: <c>readings</c> is filterable by location/metric/meterType/time range, Relay-paginated
/// with a stable cursor and a maximum page size of 100.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class ReadingsQueryTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private const int InWindowReadingCount = 15;

    // Instance fields rather than shared constants: xUnit does not serialise these Facts against
    // each other (only [Collection] members across CLASSES are serialised, not methods within one
    // class), and IAsyncLifetime.InitializeAsync runs once per test method against the one real
    // Postgres container the whole collection shares - a shared location string would let two
    // concurrently-running Facts race to insert the same (location, meter_type) row.
    private readonly string _targetLocation = $"readings-query-room-{Guid.NewGuid():N}";
    private readonly string _otherLocation = $"readings-query-other-room-{Guid.NewGuid():N}";

    private DateTimeOffset _anchor;

    public async Task InitializeAsync()
    {
        _anchor = DateTimeOffset.UtcNow;
        await using var context = await ProcessorSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);

        // 15 readings one minute apart, all inside the one-hour window the tests query for.
        var targetMeterId = await ProcessorSchemaSeed.SeedMeterWithReadingsAsync(
            context, _targetLocation, "energy", "energy", InWindowReadingCount, _anchor);

        // Same meter, but three hours older: must be excluded by the time-range filter.
        await ProcessorSchemaSeed.SeedReadingsForExistingMeterAsync(
            context, targetMeterId, "energy", count: 1, _anchor.AddHours(-3));

        // A different location entirely: must be excluded by the location filter.
        await ProcessorSchemaSeed.SeedMeterWithReadingsAsync(
            context, _otherLocation, "energy", "energy", count: 5, _anchor);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Readings_FilteredByLocationAndOneHourWindow_ReturnsOnlyMatchingRows()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        var page = await QueryPageAsync(client, first: 100, after: null);

        var nodes = page.GetProperty("nodes");
        Assert.Equal(InWindowReadingCount, nodes.GetArrayLength());
        Assert.All(nodes.EnumerateArray(), node => Assert.Equal(_targetLocation, node.GetProperty("location").GetString()));
        Assert.False(page.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    public async Task Readings_PaginatedThroughAllPages_CursorIsStableAndCoversEveryRowExactlyOnce()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        var seenObservedAt = new HashSet<DateTimeOffset>();
        string? cursor = null;
        bool hasNextPage;

        do
        {
            var page = await QueryPageAsync(client, first: 5, after: cursor);
            var nodes = page.GetProperty("nodes");

            Assert.True(nodes.GetArrayLength() <= 5);
            foreach (var node in nodes.EnumerateArray())
            {
                // A stable cursor never repeats a row across pages.
                Assert.True(seenObservedAt.Add(node.GetProperty("observedAt").GetDateTimeOffset()));
            }

            var pageInfo = page.GetProperty("pageInfo");
            hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
            cursor = pageInfo.GetProperty("endCursor").GetString();
        }
        while (hasNextPage);

        Assert.Equal(InWindowReadingCount, seenObservedAt.Count);
    }

    [Fact]
    public async Task Readings_RequestingPageSizeAboveTheMaximum_ReturnsValidationErrorInsteadOfData()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        const string query = """
            query Readings($first: Int) {
              readings(first: $first) {
                nodes { location }
              }
            }
            """;

        var body = await GraphQlClient.PostAsync(client, query, new { first = 1000 });

        Assert.True(body.TryGetProperty("errors", out var errors), body.ToString());
        Assert.True(errors.GetArrayLength() > 0);
    }

    private async Task<JsonElement> QueryPageAsync(HttpClient client, int first, string? after)
    {
        const string query = """
            query Readings($location: String!, $since: DateTime!, $until: DateTime!, $first: Int, $after: String) {
              readings(
                where: { location: { eq: $location }, observedAt: { gte: $since, lte: $until } }
                first: $first
                after: $after
              ) {
                nodes { location metricCode observedAt valueNumeric }
                pageInfo { hasNextPage endCursor }
              }
            }
            """;

        var body = await GraphQlClient.PostAsync(
            client,
            query,
            new
            {
                location = _targetLocation,
                since = _anchor.AddHours(-1),
                until = _anchor.AddMinutes(1),
                first,
                after,
            });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        return body.GetProperty("data").GetProperty("readings");
    }
}
