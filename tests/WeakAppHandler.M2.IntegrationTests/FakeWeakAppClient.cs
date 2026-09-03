using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.M2.IntegrationTests;

/// <summary>
/// Stands in for the resilience-wrapped client so a poll's outcome can be dictated directly — the
/// real HTTP-to-outcome mapping is <c>WeakAppHandler.Ingestor.Tests.WeakAppClientResilienceTests</c>'
/// job (TASK-015); this suite is about what the rest of the pipeline does with an outcome once the
/// resilience pipeline has already produced one. Once the script is exhausted the last entry repeats.
/// </summary>
internal sealed class FakeWeakAppClient(params WeakAppFetchResult[] results) : IWeakAppClient
{
    private int _index;

    public Task<WeakAppFetchResult> GetMetersAsync(CancellationToken cancellationToken)
    {
        var result = results[Math.Min(_index, results.Length - 1)];
        _index++;
        return Task.FromResult(result);
    }
}
