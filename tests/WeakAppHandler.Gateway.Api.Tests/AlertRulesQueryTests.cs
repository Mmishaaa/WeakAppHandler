using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-032: <c>alertRules</c> reads Notification's alert_rules table, including the five seed rules
/// TASK-027's migration always applies - which is exactly why every assertion here filters down to
/// this test's own rule by name rather than asserting on the total row count.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertRulesQueryTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private string _metricCode = string.Empty;
    private Guid _ruleId;

    public async Task InitializeAsync()
    {
        await using var context = await NotificationSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);

        _metricCode = NotificationSchemaSeed.NewMetricCode();
        _ruleId = await NotificationSchemaSeed.AddRuleAsync(context, _metricCode, severity: "info");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AlertRules_FilteredByMetricCode_ReturnsThisTestsOwnRule()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        const string query = """
            query AlertRules($metricCode: String!) {
              alertRules(where: { metricCode: { eq: $metricCode } }) {
                id
                metricCode
                operator
                thresholdNumeric
                severity
                hysteresisPercent
                cooldownSeconds
                isEnabled
              }
            }
            """;

        var body = await GraphQlClient.PostAsync(client, query, new { metricCode = _metricCode });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var rules = body.GetProperty("data").GetProperty("alertRules");
        Assert.Equal(1, rules.GetArrayLength());

        var rule = rules[0];
        Assert.Equal(_ruleId, rule.GetProperty("id").GetGuid());
        Assert.Equal(_metricCode, rule.GetProperty("metricCode").GetString());
        Assert.Equal("GT", rule.GetProperty("operator").GetString());
        Assert.Equal(1000m, rule.GetProperty("thresholdNumeric").GetDecimal());
        Assert.Equal("INFO", rule.GetProperty("severity").GetString());
        Assert.True(rule.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task AlertRules_Unfiltered_IncludesTheFiveSeedRulesAlongsideThisTestsOwnRule()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        var body = await GraphQlClient.PostAsync(client, "{ alertRules { id metricCode } }");

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var rules = body.GetProperty("data").GetProperty("alertRules");

        // >= 6: the five PRD §6.6 seed rules TASK-027 always applies, plus this test's own - not an
        // exact count, since another test class's rules live in the same shared table.
        Assert.True(rules.GetArrayLength() >= 6, rules.ToString());
        Assert.Contains(rules.EnumerateArray(), r => r.GetProperty("id").GetGuid() == _ruleId);
    }
}
