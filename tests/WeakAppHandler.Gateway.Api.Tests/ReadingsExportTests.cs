using System.Net;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-026's <c>GET /api/v1/readings/export</c>: streams matching readings as CSV. The streaming
/// itself (constant memory via <c>AsAsyncEnumerable</c>, never <c>ToListAsync</c>) is a property of
/// <c>ReadingsExportController</c>'s implementation rather than something a response-body assertion
/// can observe directly - what these tests prove is that the resulting CSV is correct: the right
/// rows, filtered the right way, in the right shape, for a large-enough row count that a per-row bug
/// (rather than an off-by-one on the first/last row alone) would show up.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class ReadingsExportTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private const int InWindowReadingCount = 250;

    private readonly string _targetLocation = $"export-room-{Guid.NewGuid():N}";
    private readonly string _otherLocation = $"export-other-room-{Guid.NewGuid():N}";

    private DateTimeOffset _anchor;

    public async Task InitializeAsync()
    {
        _anchor = DateTimeOffset.UtcNow;
        await using var context = await ProcessorSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);

        // A large-enough run (one minute apart) that a per-row streaming bug, not just an
        // off-by-one on the first/last row, would show up in the row count.
        var targetMeterId = await ProcessorSchemaSeed.SeedMeterWithReadingsAsync(
            context, _targetLocation, "energy", "energy", InWindowReadingCount, _anchor);

        // Well outside the in-window run above: must be excluded by the from/to window.
        await ProcessorSchemaSeed.SeedReadingsForExistingMeterAsync(
            context, targetMeterId, "energy", count: 1, _anchor.AddMinutes(-(InWindowReadingCount + 60)));

        // A different location entirely: must be excluded by the location filter.
        await ProcessorSchemaSeed.SeedMeterWithReadingsAsync(
            context, _otherLocation, "energy", "energy", count: 5, _anchor);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Export_FilteredByLocationAndTimeWindow_StreamsExactlyTheMatchingRowsAsCsv()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        using var response = await client.GetAsync(
            $"/api/v1/readings/export?location={Uri.EscapeDataString(_targetLocation)}" +
            $"&from={Uri.EscapeDataString(_anchor.AddMinutes(-InWindowReadingCount).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(_anchor.AddMinutes(1).ToString("O"))}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var (header, rows) = await ReadCsvAsync(response);

        Assert.Equal("id,meterId,location,meterType,metricCode,observedAt,valueNumeric,valueBool,isChanged", header);
        Assert.Equal(InWindowReadingCount, rows.Count);
        Assert.All(rows, row => Assert.Equal(_targetLocation, row[2]));
        Assert.All(rows, row => Assert.Equal("energy", row[4]));
    }

    [Fact]
    public async Task Export_WithNoFilters_IncludesReadingsFromEveryLocation()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        using var response = await client.GetAsync("/api/v1/readings/export");
        response.EnsureSuccessStatusCode();

        var (_, rows) = await ReadCsvAsync(response);
        var locations = rows.Select(row => row[2]).ToHashSet();

        Assert.Contains(_targetLocation, locations);
        Assert.Contains(_otherLocation, locations);
    }

    /// <summary>
    /// TASK-042: the export is a Viewer-policy read, so an anonymous caller must be refused rather
    /// than handed a CSV of every reading in the database.
    /// </summary>
    [Fact]
    public async Task Export_WithoutAToken_IsRejectedWithUnauthorized()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/readings/export");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(string Header, IReadOnlyList<string[]> Rows)> ReadCsvAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var header = lines[0];
        var rows = lines.Skip(1).Select(line => line.Split(',')).ToList();

        return (header, rows);
    }
}
