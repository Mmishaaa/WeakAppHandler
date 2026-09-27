using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Resolvers;

namespace GraphQlGateway.API.Diagnostics;

internal sealed partial class GraphQlDiagnosticEventListener(ILogger<GraphQlDiagnosticEventListener> logger)
    : ExecutionDiagnosticEventListener
{
    public override void ResolverError(IMiddlewareContext context, IError error)
        => LogResolverError(logger, error.Path?.ToString() ?? "-", error.Message, error.Exception);

    public override void ResolverError(RequestContext context, ISelection selection, IError error)
        => LogResolverError(logger, error.Path?.ToString() ?? "-", error.Message, error.Exception);

    public override void RequestError(RequestContext context, Exception error)
        => LogRequestError(logger, error);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "GraphQL resolver failed at {Path}: {ErrorMessage}")]
    private static partial void LogResolverError(
        ILogger logger,
        string path,
        string errorMessage,
        Exception? exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "GraphQL request failed.")]
    private static partial void LogRequestError(ILogger logger, Exception exception);
}
