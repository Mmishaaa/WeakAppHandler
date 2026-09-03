using WeakAppHandler.Gateway.Api.Telemetry;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-044's Gateway metrics in isolation, no HTTP pipeline involved: <see cref="GatewayMetrics"/>
/// only wraps a <see cref="System.Diagnostics.Metrics.Meter"/>, so its own correctness is a pure unit
/// test. That the real middleware calls it for a real GraphQL request is
/// <see cref="GraphQLMetricsMiddlewareTests"/>'s job.
/// </summary>
public sealed class GatewayMetricsTests
{
    [Fact]
    public void RecordRequest_TagsTheMeasurementWithMethodAndStatusCode()
    {
        using var metrics = new GatewayMetrics();
        using var listener = new MeterListenerFixture(metrics.Meter);

        metrics.RecordRequest("POST", 200, 15.5);

        var measurement = listener.DoubleMeasurements.Single(m => m.Instrument == "gateway.graphql.request.duration");
        Assert.Equal(15.5, measurement.Value);
        Assert.Equal("POST", measurement.Tags.Single(t => t.Key == "http.method").Value);
        Assert.Equal(200, measurement.Tags.Single(t => t.Key == "http.status_code").Value);
    }
}
