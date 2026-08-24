using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WeakAppHandler.ServiceDefaults.Tests;

public class HealthChecksEndpointTests
{
    [Fact]
    public async Task LiveEndpoint_ReturnsHealthy_WithoutAnyRegisteredDependency()
    {
        await using var app = await BuildAppAsync(isReadyDependencyHealthy: true);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyEndpoint_ReturnsHealthy_WhenRegisteredDependencyIsAvailable()
    {
        await using var app = await BuildAppAsync(isReadyDependencyHealthy: true);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyEndpoint_ReturnsUnhealthy_WhenRegisteredDependencyIsUnavailable()
    {
        await using var app = await BuildAppAsync(isReadyDependencyHealthy: false);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task MetricsEndpoint_ExposesPrometheusScrapeFormat()
    {
        await using var app = await BuildAppAsync(isReadyDependencyHealthy: true);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<WebApplication> BuildAppAsync(bool isReadyDependencyHealthy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddServiceDefaults();
        builder.Services.AddHealthChecks()
            .AddCheck(
                "fake-dependency",
                () => isReadyDependencyHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(),
                tags: ["ready"]);

        var app = builder.Build();
        app.MapServiceDefaultsEndpoints();
        await app.StartAsync();

        return app;
    }
}
