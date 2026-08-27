namespace WeakAppHandler.Ingestor.WeakApp;

/// <summary>
/// Resilient client for WeakApp's <c>GET /meters</c> endpoint. Every call returns a classified
/// <see cref="WeakAppFetchResult"/> instead of throwing - retries, rate-limiting and circuit-breaking
/// happen transparently inside the wrapped <see cref="HttpClient"/>.
/// </summary>
public interface IWeakAppClient
{
    public Task<WeakAppFetchResult> GetMetersAsync(CancellationToken cancellationToken);
}
