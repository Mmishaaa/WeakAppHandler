using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-044: a real GraphQL HTTP request through the real Gateway host, scraped back off its own
/// <c>/metrics</c> endpoint - not a MeterListener - because <see cref="GraphQLMetricsMiddleware"/>'s
/// job is specifically to make the measurement reachable through the same Prometheus scrape a real
/// deployment relies on, which a listener attached from outside the host would not prove.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class GraphQLMetricsMiddlewareTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task GraphQLRequest_ThenScrapingMetrics_ReportsRequestDurationTaggedWithMethodAndStatus()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        const string query = """
            query {
              meters {
                id
              }
            }
            """;

        await GraphQlClient.PostAsync(client, query);

        var metricsResponse = await client.GetAsync("/metrics");
        var body = await metricsResponse.Content.ReadAsStringAsync();

        Assert.Contains("gateway_graphql_request_duration", body, StringComparison.Ordinal);
        Assert.Contains("http_method=\"POST\"", body, StringComparison.Ordinal);
        Assert.Contains("http_status_code=\"200\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthCheckRequest_ThenScrapingMetrics_DoesNotRecordAGraphQLRequestDuration()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        await client.GetAsync("/health/live");

        var metricsResponse = await client.GetAsync("/metrics");
        var body = await metricsResponse.Content.ReadAsStringAsync();

        // The middleware only times /graphql requests; a health check hitting a completely different
        // path must not be attributed to the GraphQL request-duration histogram (which, having never
        // recorded a single measurement in this factory's lifetime, does not appear in the scrape at
        // all - the absence of a GET-tagged series is the meaningful assertion here).
        Assert.DoesNotContain("http_method=\"GET\"", body, StringComparison.Ordinal);
    }
}
