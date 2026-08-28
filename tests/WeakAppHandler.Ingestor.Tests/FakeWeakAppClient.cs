using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// Stands in for the resilience-wrapped client so a poll's outcome can be dictated directly.
/// <see cref="WeakAppClientResilienceTests"/> already covers how a real HTTP exchange becomes a
/// <see cref="WeakAppFetchResult"/>; the polling tests are about what the poller does with one.
/// Once the script is exhausted the last entry repeats.
/// </summary>
internal sealed class FakeWeakAppClient(params WeakAppFetchResult[] results) : IWeakAppClient
{
    private int _index;

    public int CallCount { get; private set; }

    public Task<WeakAppFetchResult> GetMetersAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        var result = results[Math.Min(_index, results.Length - 1)];
        _index++;
        return Task.FromResult(result);
    }
}
