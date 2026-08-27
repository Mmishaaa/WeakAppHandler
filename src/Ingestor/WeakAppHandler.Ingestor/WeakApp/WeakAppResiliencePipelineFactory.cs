using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace WeakAppHandler.Ingestor.WeakApp;

/// <summary>
/// Builds the four-strategy resilience pipeline required by PRD §6.1: an overall time budget
/// shorter than the polling interval, retry with exponential backoff and jitter (honouring
/// <c>Retry-After</c> exactly on HTTP 429), a circuit breaker, and a per-attempt timeout.
/// Exposed as a standalone <c>Configure</c> method (rather than only a DI callback) so unit tests
/// can build the exact same pipeline against a <see cref="TimeProvider"/> they control.
/// </summary>
public static class WeakAppResiliencePipelineFactory
{
    public static void Configure(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        WeakAppOptions options,
        TimeProvider timeProvider)
    {
        builder.TimeProvider = timeProvider;

        // Outermost: the total budget for one poll, including every retry. Kept below the polling
        // interval by WeakAppOptionsValidator so overlapping polls can never happen.
        builder.AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));

        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = args => ValueTask.FromResult(IsTransientFailure(args.Outcome)),
            MaxRetryAttempts = options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(200),
            DelayGenerator = args => ValueTask.FromResult(GetRetryAfterDelay(args.Outcome, timeProvider)),
        });

        // Wraps the retry loop: once open, requests fail fast without ever reaching WeakApp.
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = args => ValueTask.FromResult(IsTransientFailure(args.Outcome)),
            FailureRatio = options.CircuitBreakerFailureRatio,
            SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds),
            MinimumThroughput = options.CircuitBreakerMinimumThroughput,
            BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds),
        });

        // Innermost: bounds a single attempt so one slow/hanging call can't consume the whole budget.
        builder.AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
    }

    private static bool IsTransientFailure(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or IOException)
        {
            return true;
        }

        if (outcome.Result is not { } response)
        {
            return false;
        }

        return (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;
    }

    private static TimeSpan? GetRetryAfterDelay(Outcome<HttpResponseMessage> outcome, TimeProvider timeProvider)
    {
        if (outcome.Result is not { StatusCode: HttpStatusCode.TooManyRequests } response)
        {
            return null;
        }

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var now = timeProvider.GetUtcNow();
            return date > now ? date - now : TimeSpan.Zero;
        }

        // No Retry-After header on the 429: fall back to the configured exponential backoff.
        return null;
    }
}
