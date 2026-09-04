using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-032: <c>alerts</c> is filterable by status/severity/location/time and reverse-chronological
/// by default, reading Notification's alerting schema rather than the core schema.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertsQueryTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private readonly string _location = $"alerts-query-room-{Guid.NewGuid():N}";
    private readonly string _otherLocation = $"alerts-query-other-room-{Guid.NewGuid():N}";
    private readonly DateTimeOffset _anchor = DateTimeOffset.UtcNow;

    private string _metricCode = string.Empty;
    private Guid _resolvedAlertId;
    private Guid _activeAlertId;

    public async Task InitializeAsync()
    {
        await using var context = await NotificationSchemaSeed.CreateMigratedContextAsync(fixture.Postgres.ConnectionString);

        _metricCode = NotificationSchemaSeed.NewMetricCode();
        var ruleId = await NotificationSchemaSeed.AddRuleAsync(context, _metricCode, severity: "critical");

        // Older, resolved, in the target location - must survive the status/location filters.
        _resolvedAlertId = await NotificationSchemaSeed.AddAlertAsync(
            context,
            ruleId,
            _metricCode,
            Guid.NewGuid(),
            _location,
            "air_quality",
            _anchor.AddMinutes(-10),
            triggeredValueNumeric: 1200m,
            severity: "critical",
            status: "resolved",
            resolvedAt: _anchor.AddMinutes(-5),
            resolvedValueNumeric: 900m);

        // Newer, active, in the target location - must appear first under the default sort.
        _activeAlertId = await NotificationSchemaSeed.AddAlertAsync(
            context,
            ruleId,
            _metricCode,
            Guid.NewGuid(),
            _location,
            "air_quality",
            _anchor,
            triggeredValueNumeric: 1500m,
            severity: "critical",
            status: "active");

        // Same rule, different location - must be excluded by the location filter.
        await NotificationSchemaSeed.AddAlertAsync(
            context,
            ruleId,
            _metricCode,
            Guid.NewGuid(),
            _otherLocation,
            "air_quality",
            _anchor,
            triggeredValueNumeric: 1300m,
            severity: "critical",
            status: "active");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Alerts_FilteredByLocation_ReturnsOnlyThatLocationNewestFirst()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        const string query = """
            query Alerts($location: String!) {
              alerts(where: { location: { eq: $location } }) {
                nodes {
                  id
                  status
                  severity
                  triggeredAt
                  triggeredValueNumeric
                  resolvedAt
                  resolvedValueNumeric
                }
              }
            }
            """;

        var body = await GraphQlClient.PostAsync(client, query, new { location = _location });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var nodes = body.GetProperty("data").GetProperty("alerts").GetProperty("nodes");
        Assert.Equal(2, nodes.GetArrayLength());

        // Newest first: the active alert (triggered at _anchor) before the resolved one (_anchor - 10m).
        Assert.Equal(_activeAlertId, nodes[0].GetProperty("id").GetGuid());
        Assert.Equal("ACTIVE", nodes[0].GetProperty("status").GetString());
        Assert.Equal(1500m, nodes[0].GetProperty("triggeredValueNumeric").GetDecimal());

        Assert.Equal(_resolvedAlertId, nodes[1].GetProperty("id").GetGuid());
        Assert.Equal("RESOLVED", nodes[1].GetProperty("status").GetString());
        Assert.Equal(900m, nodes[1].GetProperty("resolvedValueNumeric").GetDecimal());
    }

    [Fact]
    public async Task Alerts_FilteredByStatusActive_ExcludesResolvedAlerts()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        const string query = """
            query Alerts($location: String!) {
              alerts(where: { location: { eq: $location }, status: { eq: ACTIVE } }) {
                nodes { id status }
              }
            }
            """;

        var body = await GraphQlClient.PostAsync(client, query, new { location = _location });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var nodes = body.GetProperty("data").GetProperty("alerts").GetProperty("nodes");
        Assert.Equal(1, nodes.GetArrayLength());
        Assert.Equal(_activeAlertId, nodes[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Alerts_FilteredBySeverityAndTriggeredAtRange_ReturnsOnlyMatchingRows()
    {
        await using var factory = await GatewayApiFactory.CreateAsync(fixture.Postgres.ConnectionString, fixture.RabbitMq.ConnectionString);
        using var client = factory.CreateAuthenticatedClient(factory.ViewerToken);

        const string query = """
            query Alerts($location: String!, $severity: AlertSeverity!, $since: DateTime!) {
              alerts(where: { location: { eq: $location }, severity: { eq: $severity }, triggeredAt: { gte: $since } }) {
                nodes { id }
              }
            }
            """;

        var body = await GraphQlClient.PostAsync(
            client,
            query,
            new { location = _location, severity = "CRITICAL", since = _anchor.AddMinutes(-1) });

        Assert.False(body.TryGetProperty("errors", out _), body.ToString());

        var nodes = body.GetProperty("data").GetProperty("alerts").GetProperty("nodes");
        Assert.Equal(1, nodes.GetArrayLength());
        Assert.Equal(_activeAlertId, nodes[0].GetProperty("id").GetGuid());
    }
}
