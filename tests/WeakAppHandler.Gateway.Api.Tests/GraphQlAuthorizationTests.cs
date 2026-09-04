using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-042: the Gateway's GraphQL surface had no inbound authorization at all before this task -
/// <c>[Authorize(ServicePolicies.Viewer)]</c> on the <c>Query</c> type is what closes that, and these
/// tests are the pair that proves it: refused without a token, answered with one. Both run against
/// the real Auth Service (see <see cref="GatewayApiFactory"/>), so the token being accepted is a
/// real JWKS validation rather than a stubbed principal.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class GraphQlAuthorizationTests(IntegrationTestFixture fixture)
{
    private const string MetersQuery = "{ meters { id } }";

    /// <summary>
    /// Per the GraphQL-over-HTTP spec, a request using the <c>application/json</c> response content
    /// type always answers with HTTP 200, even when execution fails outright - so a type-level
    /// <c>[Authorize]</c> denial surfaces as a GraphQL error in the response body (code
    /// <c>AUTH_NOT_AUTHENTICATED</c>, null data), not as an HTTP 401. This is confirmed by
    /// HotChocolate's own authorization test suite, which asserts <c>HttpStatusCode.OK</c> for every
    /// such case.
    /// </summary>
    [Fact]
    public async Task Query_WithoutAToken_IsRejectedWithAnAuthenticationError()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(
            fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await GraphQlClient.PostRawAsync(client, MetersQuery);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.TryGetProperty("errors", out var errors), body.ToString());
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("AUTH_NOT_AUTHENTICATED", code);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Query_WithAViewerToken_Succeeds()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(
            fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        using var response = await GraphQlClient.PostRawAsync(client, MetersQuery);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.TryGetProperty("errors", out _), body.ToString());
    }
}
