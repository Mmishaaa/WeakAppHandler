using System.Diagnostics.Metrics;

namespace WeakAppHandler.Gateway.Api.Telemetry;

/// <summary>
/// The Gateway's domain metrics (TASK-044, PRD §6 F10): GraphQL request duration. Recorded by
/// <see cref="GraphQLMetricsMiddleware"/> around <c>/graphql</c> HTTP requests only - a subscription's
/// WebSocket connection can live far longer than any one operation it carries, so timing the whole
/// connection would mix "how long a client stayed subscribed" into a metric meant to answer "how long
/// did a query/mutation take".
/// </summary>
public sealed class GatewayMetrics : IDisposable
{
    public const string MeterName = "WeakAppHandler.Gateway";

    private readonly Meter _meter;
    private readonly Histogram<double> _requestDuration;

    public GatewayMetrics()
    {
        _meter = new Meter(MeterName);
        _requestDuration = _meter.CreateHistogram<double>(
            "gateway.graphql.request.duration",
            unit: "ms",
            description: "Duration of a GraphQL HTTP request, tagged by HTTP method and status code.");
    }

    /// <summary>
    /// Exposed so tests can attach a <see cref="MeterListener"/> scoped to this exact instance rather
    /// than by meter name - multiple <see cref="GatewayMetrics"/> instances share the same name across
    /// parallel test hosts, and a listener filtering by name alone would pick up another test's
    /// measurements too.
    /// </summary>
    public Meter Meter => _meter;

    public void RecordRequest(string httpMethod, int statusCode, double durationMs) =>
        _requestDuration.Record(
            durationMs,
            new KeyValuePair<string, object?>("http.method", httpMethod),
            new KeyValuePair<string, object?>("http.status_code", statusCode));

    public void Dispose() => _meter.Dispose();
}
