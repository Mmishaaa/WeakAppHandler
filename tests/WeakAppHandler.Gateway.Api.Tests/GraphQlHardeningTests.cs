using WeakAppHandler.Gateway.Api.GraphQL;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>TASK-025: query-shape limits enforced independently of authentication.</summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class GraphQlHardeningTests(IntegrationTestFixture fixture)
{
    /// <summary>
    /// The classic introspection-recursion query: <c>__Field.type</c> and <c>__Type.ofType</c> both
    /// return <c>__Type</c>, so this can be nested to any depth without the schema itself needing a
    /// single recursive field. Twelve levels of <c>ofType</c> puts the document well past
    /// <see cref="GraphQLSecurityLimits.MaxExecutionDepth"/>.
    /// </summary>
    private const string DeeplyNestedIntrospectionQuery =
        """
        {
          __schema {
            queryType {
              fields {
                type {
                  ofType { ofType { ofType { ofType { ofType { ofType { ofType { ofType { ofType { ofType { name } } } } } } } } } }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// Run in Development (where <see cref="GraphQlHardeningTests"/>'s introspection-disabled test
    /// does not apply) so a rejection here is unambiguously the depth rule, not the separate
    /// introspection guard rejecting the query for an unrelated reason.
    /// </summary>
    [Fact]
    public async Task DeeplyNestedQuery_IsRejectedWithAnExecutionDepthError()
    {
        using var factory = GatewayApiFactory.Create(
            fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString, environment: "Development");
        using var client = factory.CreateClient();

        var body = await GraphQlClient.PostExpectingRejectionAsync(client, DeeplyNestedIntrospectionQuery);

        Assert.True(body.TryGetProperty("errors", out var errors), body.ToString());
        var error = Assert.Single(errors.EnumerateArray());
        Assert.Contains("execution depth", error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            GraphQLSecurityLimits.MaxExecutionDepth,
            error.GetProperty("extensions").GetProperty("allowedExecutionDepth").GetInt32());
    }

    [Fact]
    public async Task Introspection_InAProductionLikeEnvironment_IsRefused()
    {
        using var factory = GatewayApiFactory.Create(
            fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString, environment: "Production");
        using var client = factory.CreateClient();

        var body = await GraphQlClient.PostExpectingRejectionAsync(client, "{ __schema { queryType { name } } }");

        Assert.True(body.TryGetProperty("errors", out var errors), body.ToString());
        var error = Assert.Single(errors.EnumerateArray());
        Assert.Contains("introspection", error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Introspection_InDevelopment_Succeeds()
    {
        using var factory = GatewayApiFactory.Create(
            fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString, environment: "Development");
        using var client = factory.CreateClient();

        var body = await GraphQlClient.PostAsync(client, "{ __schema { queryType { name } } }");

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());
        Assert.Equal("Query", body.GetProperty("data").GetProperty("__schema").GetProperty("queryType").GetProperty("name").GetString());
    }

    /// <summary>
    /// An ordinary, shallow query for real data must not be caught by the same limit that rejects
    /// the pathological ones above - the criterion is about intentionally deep queries, not queries
    /// in general.
    /// </summary>
    [Fact]
    public async Task OrdinaryShallowQuery_IsNotAffectedByTheDepthLimit()
    {
        using var factory = GatewayApiFactory.Create(
            fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString, environment: "Development");
        using var client = factory.CreateClient();

        var body = await GraphQlClient.PostAsync(client, "{ meters { id location currentValues { metricCode } } }");

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());
    }
}
