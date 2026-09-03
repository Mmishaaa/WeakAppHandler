using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace WeakAppHandler.Gateway.Api.Telemetry;

/// <summary>
/// Times every non-WebSocket request to <c>/graphql</c> and records it on <see cref="GatewayMetrics"/>.
/// A plain ASP.NET Core middleware rather than a HotChocolate execution-diagnostics hook: it needs no
/// dependency on HotChocolate's own instrumentation surface, and query/mutation execution is entirely
/// synchronous within the one HTTP request HotChocolate's endpoint handles it in.
/// </summary>
public sealed class GraphQLMetricsMiddleware(RequestDelegate next, GatewayMetrics metrics)
{
    private const string GraphQLPath = "/graphql";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Path.StartsWithSegments(GraphQLPath) || context.WebSockets.IsWebSocketRequest)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        await next(context).ConfigureAwait(false);
        metrics.RecordRequest(
            context.Request.Method,
            context.Response.StatusCode,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
