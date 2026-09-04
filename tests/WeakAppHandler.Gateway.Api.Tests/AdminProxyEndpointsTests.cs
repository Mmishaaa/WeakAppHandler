using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-026's acceptance criterion that a proxied status/stats response is the same data a direct
/// call to the Ingestor/Processor would return - proven by making both calls against the same real
/// hosts and comparing the bodies, not by asserting against a hand-written expected shape that could
/// silently drift from what the admin APIs actually return.
/// </summary>
/// <remarks>
/// TASK-042 put the Admin policy on the proxy itself, so the Gateway-side calls below now carry the
/// host's admin token - and the last two tests cover the refusals that policy exists for.
/// </remarks>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AdminProxyEndpointsTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private readonly string _virtualHost = $"task026-{Guid.NewGuid():N}";

    public Task InitializeAsync() => fixture.RabbitMq.CreateVirtualHostAsync(_virtualHost);

    public Task DisposeAsync() => fixture.RabbitMq.DeleteVirtualHostAsync(_virtualHost);

    [Fact]
    public async Task IngestionStatus_ProxiedThroughTheGateway_MatchesADirectCallToTheIngestor()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        var direct = await GetJsonAsync(host.IngestorClient, "/api/v1/ingestion/status", host.MachineToken);
        var proxied = await GetJsonAsync(host.Client, "/api/v1/ingestion/status", host.AdminToken);

        Assert.Equal(HttpStatusCode.OK, direct.Status);
        Assert.Equal(HttpStatusCode.OK, proxied.Status);
        Assert.Equal(direct.Body, proxied.Body);
    }

    [Fact]
    public async Task ProcessingStats_ProxiedThroughTheGateway_MatchesADirectCallToTheProcessor()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        var direct = await GetJsonAsync(host.ProcessorClient, "/api/v1/processing/stats", host.MachineToken);
        var proxied = await GetJsonAsync(host.Client, "/api/v1/processing/stats", host.AdminToken);

        Assert.Equal(HttpStatusCode.OK, direct.Status);
        Assert.Equal(HttpStatusCode.OK, proxied.Status);
        Assert.Equal(direct.Body, proxied.Body);
    }

    [Fact]
    public async Task IngestionTrigger_ProxiedThroughTheGateway_RunsAPollAndReturnsItsOutcome()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        using var response = await SendAsync(
            host.Client, HttpMethod.Post, "/api/v1/ingestion/trigger", host.AdminToken, content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Success", body.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task IngestionConfig_ProxiedThroughTheGateway_UpdatesThePollingIntervalOnTheIngestor()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        using var putResponse = await SendAsync(
            host.Client,
            HttpMethod.Put,
            "/api/v1/ingestion/config",
            host.AdminToken,
            JsonContent.Create(new { pollingIntervalSeconds = 30 }));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var (statusCode, statusBody) = await GetJsonAsync(host.Client, "/api/v1/ingestion/status", host.AdminToken);
        Assert.Equal(HttpStatusCode.OK, statusCode);
        using var status = JsonDocument.Parse(statusBody);
        Assert.Equal(30, status.RootElement.GetProperty("pollingIntervalSeconds").GetInt32());
    }

    [Fact]
    public async Task IngestionConfig_ProxiedThroughTheGateway_ForwardsTheIngestorsValidationFailure()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        // Below the test Ingestor's own TotalTimeoutSeconds (5s, see GatewayAdminProxyHost), so the
        // Ingestor itself must reject it - proving the 400 body reaching the browser is the
        // Ingestor's own validation message, not something the proxy invented.
        using var response = await SendAsync(
            host.Client,
            HttpMethod.Put,
            "/api/v1/ingestion/config",
            host.AdminToken,
            JsonContent.Create(new { pollingIntervalSeconds = 1 }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>TASK-042: no token at all is a failure to authenticate, not a failure to authorize.</summary>
    [Fact]
    public async Task IngestionTrigger_WithoutAToken_IsRejectedWithUnauthorized()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        using var response = await SendAsync(
            host.Client, HttpMethod.Post, "/api/v1/ingestion/trigger", token: null, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// TASK-042: a real, valid, correctly-signed token that simply carries the wrong role - the case
    /// a forged-token test could not distinguish from the one above.
    /// </summary>
    [Fact]
    public async Task IngestionTrigger_WithAViewerToken_IsRejectedWithForbidden()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        using var response = await SendAsync(
            host.Client, HttpMethod.Post, "/api/v1/ingestion/trigger", host.ViewerToken, content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<(HttpStatusCode Status, string Body)> GetJsonAsync(HttpClient client, string path, string? token)
    {
        using var response = await SendAsync(client, HttpMethod.Get, path, token, content: null);
        var body = await response.Content.ReadAsStringAsync();

        return (response.StatusCode, body);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, string? token, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }
}
