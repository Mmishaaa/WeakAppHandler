namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// A stand-in for WeakApp's server. Returns one canned response per call, following the supplied
/// script; once the script is exhausted it keeps repeating the last entry, so a single repeating
/// factory (e.g. "always 502") is expressed by passing exactly one.
/// </summary>
internal sealed class RecordingHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
{
    private int _index;

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var factory = responses[Math.Min(_index, responses.Length - 1)];
        _index++;
        return Task.FromResult(factory());
    }
}
