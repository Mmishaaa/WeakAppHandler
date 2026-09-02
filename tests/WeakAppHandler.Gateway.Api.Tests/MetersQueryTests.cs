using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>TASK-023: <c>meters</c> lists meters with their current values, filterable by location and meter type.</summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class MetersQueryTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private const string KitchenLocation = "meters-query-kitchen";
    private const string GarageLocation = "meters-query-garage";

    private Guid _kitchenMeterId;

    public async Task InitializeAsync()
    {
        var anchor = DateTimeOffset.UtcNow;
        await using var context = await ProcessorSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);

        _kitchenMeterId = await ProcessorSchemaSeed.SeedMeterWithReadingsAsync(
            context, KitchenLocation, "air_quality", "co2", count: 1, anchor);
        await ProcessorSchemaSeed.SeedCurrentValueAsync(context, _kitchenMeterId, "co2", valueNumeric: 812m, previousValueNumeric: 790m, anchor);

        var garageMeterId = await ProcessorSchemaSeed.SeedMeterWithReadingsAsync(
            context, GarageLocation, "motion", "motion_detected", count: 1, anchor);
        await ProcessorSchemaSeed.SeedCurrentValueAsync(context, garageMeterId, "motion_detected", valueNumeric: 0m, previousValueNumeric: null, anchor);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Meters_FilteredByLocation_ReturnsOnlyThatMeterWithItsCurrentValues()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        const string query = """
            query Meters($location: String!) {
              meters(where: { location: { eq: $location } }) {
                id
                location
                meterType
                currentValues {
                  metricCode
                  valueNumeric
                  previousValueNumeric
                }
              }
            }
            """;

        var body = await GraphQlClient.PostAsync(client, query, new { location = KitchenLocation });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var meters = body.GetProperty("data").GetProperty("meters");
        Assert.Equal(1, meters.GetArrayLength());

        var meter = meters[0];
        Assert.Equal(KitchenLocation, meter.GetProperty("location").GetString());
        Assert.Equal("air_quality", meter.GetProperty("meterType").GetString());
        Assert.Equal(_kitchenMeterId, meter.GetProperty("id").GetGuid());

        var currentValues = meter.GetProperty("currentValues");
        Assert.Equal(1, currentValues.GetArrayLength());
        Assert.Equal("co2", currentValues[0].GetProperty("metricCode").GetString());
        Assert.Equal(812m, currentValues[0].GetProperty("valueNumeric").GetDecimal());
        Assert.Equal(790m, currentValues[0].GetProperty("previousValueNumeric").GetDecimal());
    }
}
