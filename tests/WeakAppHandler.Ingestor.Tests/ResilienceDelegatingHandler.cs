using Polly;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// Runs a pre-built resilience pipeline in front of an inner <see cref="HttpMessageHandler"/>, the
/// same way <c>AddResilienceHandler</c> wires it into the real <c>HttpClient</c> pipeline - so tests
/// can exercise <see cref="WeakAppHandler.Ingestor.WeakApp.WeakAppResiliencePipelineFactory"/> without
/// going through dependency injection.
/// </summary>
internal sealed class ResilienceDelegatingHandler(ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        pipeline.ExecuteAsync(ct => new ValueTask<HttpResponseMessage>(base.SendAsync(request, ct)), cancellationToken).AsTask();
}
