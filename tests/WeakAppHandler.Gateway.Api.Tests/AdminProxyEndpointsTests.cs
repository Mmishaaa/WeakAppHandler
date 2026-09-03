using System.Net;
using System.Net.Http.Headers;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-026's acceptance criterion that a proxied status/stats response is the same data a direct
/// call to the Ingestor/Processor would return - proven by making both calls against the same real
/// hosts and comparing the bodies, not by asserting against a hand-written expected shape that could
/// silently drift from what the admin APIs actually return.
/// </summary>
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
        var proxied = await GetJsonAsync(host.Client, "/api/v1/ingestion/status", token: null);

        Assert.Equal(HttpStatusCode.OK, direct.Status);
        Assert.Equal(HttpStatusCode.OK, proxied.Status);
        Assert.Equal(direct.Body, proxied.Body);
    }

    [Fact]
    public async Task ProcessingStats_ProxiedThroughTheGateway_MatchesADirectCallToTheProcessor()
    {
        await using var host = await GatewayAdminProxyHost.StartAsync(fixture, _virtualHost);

        var direct = await GetJsonAsync(host.ProcessorClient, "/api/v1/processing/stats", host.MachineToken);
        var proxied = await GetJsonAsync(host.Client, "/api/v1/processing/stats", token: null);

        Assert.Equal(HttpStatusCode.OK, direct.Status);
        Assert.Equal(HttpStatusCode.OK, proxied.Status);
        Assert.Equal(direct.Body, proxied.Body);
    }

    private static async Task<(HttpStatusCode Status, string Body)> GetJsonAsync(HttpClient client, string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return (response.StatusCode, body);
    }
}
