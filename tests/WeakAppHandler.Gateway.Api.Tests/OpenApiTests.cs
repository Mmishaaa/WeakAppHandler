using System.Net;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-026: the Gateway's OpenAPI document is generated and browsable through Swagger UI in
/// Development, and neither is exposed once the environment leaves Development - the same
/// "Development-only" gate <see cref="GraphQlHardeningTests"/> already proves for GraphQL
/// introspection, applied here to the REST surface's own documentation.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class OpenApiTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task OpenApiDocument_InDevelopment_IsServedAsJson()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SwaggerUi_InDevelopment_ServesTheDocumentationPage()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("swagger", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwaggerUi_OutsideDevelopment_IsNotExposed()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString, environment: "Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
