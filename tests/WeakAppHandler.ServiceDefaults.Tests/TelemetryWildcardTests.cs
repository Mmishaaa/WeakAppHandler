using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WeakAppHandler.ServiceDefaults.Tests;

/// <summary>
/// TASK-044: <see cref="ServiceDefaultsExtensions"/> registers its meter/tracer providers with a
/// "WeakAppHandler.*" wildcard rather than each service's meter/source name individually, so that any
/// future service's own domain telemetry (e.g. IngestorMetrics, ProcessorMetrics) is picked up without
/// ServiceDefaults having to know about it. This proves the wildcard actually matches an arbitrary
/// "WeakAppHandler.*"-named <see cref="Meter"/> end to end, through the real Prometheus scrape
/// endpoint - not just that the SDK feature exists in the abstract.
/// </summary>
public sealed class TelemetryWildcardTests
{
    [Fact]
    public async Task MetricsEndpoint_ExposesACounterFromAnArbitraryWeakAppHandlerNamedMeter()
    {
        await using var app = await BuildAppAsync();
        using var client = app.GetTestClient();

        using var meter = new Meter("WeakAppHandler.TelemetryWildcardTests");
        var counter = meter.CreateCounter<long>("telemetrywildcardtests.probe");
        counter.Add(1);

        var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("telemetrywildcardtests_probe", body, StringComparison.Ordinal);
    }

    private static async Task<WebApplication> BuildAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddServiceDefaults();
        builder.Services.AddHealthChecks()
            .AddCheck("fake-dependency", () => HealthCheckResult.Healthy(), tags: ["ready"]);

        var app = builder.Build();
        app.MapServiceDefaultsEndpoints();
        await app.StartAsync();

        return app;
    }
}
