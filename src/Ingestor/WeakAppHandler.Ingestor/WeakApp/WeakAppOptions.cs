namespace WeakAppHandler.Ingestor.WeakApp;

/// <summary>
/// Configuration for polling WeakApp and for the resilience pipeline wrapped around each call.
/// Bound from the <c>WeakApp</c> configuration section.
/// </summary>
public sealed class WeakAppOptions
{
    public const string SectionName = "WeakApp";

    public required Uri BaseUrl { get; init; }

    public required string ApiKey { get; init; }

    public int PollingIntervalSeconds { get; init; } = 10;

    /// <summary>Timeout applied to a single HTTP attempt (innermost strategy).</summary>
    public int AttemptTimeoutSeconds { get; init; } = 3;

    /// <summary>
    /// Overall time budget for one poll, including every retry. Must stay below
    /// <see cref="PollingIntervalSeconds"/> so retries never overlap the next scheduled poll.
    /// </summary>
    public int TotalTimeoutSeconds { get; init; } = 8;

    public int MaxRetryAttempts { get; init; } = 3;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 30;

    public int CircuitBreakerMinimumThroughput { get; init; } = 4;

    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;
}
