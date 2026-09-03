using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Persistence.Configurations;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// TASK-030's acceptance criteria against the running service: an invalid request is rejected with a
/// field-level 400, a viewer token is rejected with 403, and admin CRUD is correctly reflected in
/// <c>alert_rules</c> without disturbing the seed rules.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertRulesAdminEndpointsTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private readonly string _virtualHost = $"task030-{Guid.NewGuid():N}";

    public Task InitializeAsync() => fixture.RabbitMq.CreateVirtualHostAsync(_virtualHost);

    public Task DisposeAsync() => fixture.RabbitMq.DeleteVirtualHostAsync(_virtualHost);

    [Fact]
    public async Task Create_WithAnInvalidOperator_ReturnsBadRequestWithAFieldLevelMessage()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);

        var request = ValidRequestPayload(AlertingDatabase.NewMetricCode());
        request["operator"] = "greater-than";

        using var response = await PostAsync(host, host.AdminToken, request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Operator", out _), problem.ToString());
    }

    [Fact]
    public async Task Create_WithANegativeCooldown_ReturnsBadRequestWithAFieldLevelMessage()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);

        var request = ValidRequestPayload(AlertingDatabase.NewMetricCode());
        request["cooldownSeconds"] = -1;

        using var response = await PostAsync(host, host.AdminToken, request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.GetProperty("errors").TryGetProperty("CooldownSeconds", out _), problem.ToString());
    }

    [Fact]
    public async Task Create_WithAHysteresisOutsideZeroToOneHundred_ReturnsBadRequestWithAFieldLevelMessage()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);

        var request = ValidRequestPayload(AlertingDatabase.NewMetricCode());
        request["hysteresisPercent"] = 150m;

        using var response = await PostAsync(host, host.AdminToken, request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.GetProperty("errors").TryGetProperty("HysteresisPercent", out _), problem.ToString());
    }

    [Fact]
    public async Task Create_WithBothThresholdKinds_ReturnsBadRequestWithAFieldLevelMessage()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);

        var request = ValidRequestPayload(AlertingDatabase.NewMetricCode());
        request["thresholdBool"] = true;

        using var response = await PostAsync(host, host.AdminToken, request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.GetProperty("errors").TryGetProperty("ThresholdNumeric", out _), problem.ToString());
    }

    [Fact]
    public async Task GetAll_WithAViewerToken_IsRejectedWithForbidden()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);

        using var response = await GetAsync(host, host.ViewerToken, "/api/v1/alert-rules");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithNoToken_IsRejectedWithUnauthorized()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);

        using var response = await GetAsync(host, token: null, "/api/v1/alert-rules");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithAnAdminToken_ReturnsExactlyTheSeedRuleCount()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);

        using var response = await GetAsync(host, host.AdminToken, "/api/v1/alert-rules");
        var rules = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Not an exact-length assertion: this database is shared by every test in the collection, so
        // other tests' own rules (AlertingDatabase.NewRule, unrelated to this endpoint) can be present
        // too. What TASK-030 promises is that every seed rule is still there, unduplicated.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returnedIds = rules.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();
        Assert.All(AlertRuleSeedData.All, seed => Assert.Single(returnedIds, id => id == seed.Id));
    }

    [Fact]
    public async Task CreateUpdateDelete_WithAnAdminToken_IsCorrectlyReflectedInAlertRulesWithoutDuplicatingSeedRules()
    {
        await using var host = await AlertRulesAdminHost.StartAsync(fixture, _virtualHost);
        var metricCode = AlertingDatabase.NewMetricCode();

        using var createResponse = await PostAsync(host, host.AdminToken, ValidRequestPayload(metricCode));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ruleId = created.GetProperty("id").GetGuid();

        await using (var db = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString))
        {
            // Scoped to this test's own unique metric code rather than a total row count: the
            // database is shared across the whole collection, so other tests' own rules are also
            // present and would make a total-count assertion flaky.
            var withThisMetricCode = await db.AlertRules.CountAsync(r => r.MetricCode == metricCode);
            Assert.Equal(1, withThisMetricCode);

            var stored = await db.AlertRules.SingleAsync(r => r.Id == ruleId);
            Assert.Equal(metricCode, stored.MetricCode);
            Assert.Equal(1000m, stored.ThresholdNumeric);
        }

        var updateRequest = ValidRequestPayload(metricCode);
        updateRequest["thresholdNumeric"] = 2000m;
        using var updateResponse = await PutAsync(host, host.AdminToken, $"/api/v1/alert-rules/{ruleId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        await using (var db = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString))
        {
            var stored = await db.AlertRules.SingleAsync(r => r.Id == ruleId);
            Assert.Equal(2000m, stored.ThresholdNumeric);
        }

        using var deleteResponse = await DeleteAsync(host, host.AdminToken, $"/api/v1/alert-rules/{ruleId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using (var db = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString))
        {
            Assert.False(await db.AlertRules.AnyAsync(r => r.MetricCode == metricCode));
            Assert.False(await db.AlertRules.AnyAsync(r => r.Id == ruleId));

            // The seed rules themselves must still be untouched by this test's create/update/delete.
            foreach (var seed in AlertRuleSeedData.All)
            {
                Assert.Equal(1, await db.AlertRules.CountAsync(r => r.Id == seed.Id));
            }
        }
    }

    private static Dictionary<string, object?> ValidRequestPayload(string metricCode) => new()
    {
        ["name"] = $"test-rule-{Guid.NewGuid():N}",
        ["location"] = null,
        ["meterType"] = null,
        ["metricCode"] = metricCode,
        ["operator"] = "gt",
        ["thresholdNumeric"] = 1000m,
        ["thresholdBool"] = null,
        ["severity"] = "warning",
        ["hysteresisPercent"] = null,
        ["cooldownSeconds"] = null,
        ["isEnabled"] = null,
    };

    private static Task<HttpResponseMessage> PostAsync(AlertRulesAdminHost host, string? token, object request) =>
        SendAsync(host, HttpMethod.Post, "/api/v1/alert-rules", token, request);

    private static Task<HttpResponseMessage> PutAsync(AlertRulesAdminHost host, string? token, string path, object request) =>
        SendAsync(host, HttpMethod.Put, path, token, request);

    private static Task<HttpResponseMessage> GetAsync(AlertRulesAdminHost host, string? token, string path) =>
        SendAsync(host, HttpMethod.Get, path, token, content: null);

    private static Task<HttpResponseMessage> DeleteAsync(AlertRulesAdminHost host, string? token, string path) =>
        SendAsync(host, HttpMethod.Delete, path, token, content: null);

    private static async Task<HttpResponseMessage> SendAsync(
        AlertRulesAdminHost host, HttpMethod method, string path, string? token, object? content)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = content is null ? null : JsonContent.Create(content),
        };

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await host.Client.SendAsync(request);
    }
}
