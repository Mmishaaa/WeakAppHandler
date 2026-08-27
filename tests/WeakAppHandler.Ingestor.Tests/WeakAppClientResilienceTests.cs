using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// Exercises <see cref="WeakAppResiliencePipelineFactory"/> through a real <see cref="WeakAppClient"/>
/// wired to a scripted fake backend (see <see cref="RecordingHandler"/>), matching TASK-015's three
/// acceptance criteria: transient-error retry with backoff, honouring <c>Retry-After</c> on HTTP 429,
/// and the circuit breaker opening (and staying fail-fast) after repeated failures.
/// </summary>
public sealed class WeakAppClientResilienceTests
{
    [Fact]
    public async Task GetMetersAsync_TransientServerErrorThenSuccess_RetriesAndRecovers()
    {
        var handler = new RecordingHandler(
            TestResponses.BadGateway(),
            TestResponses.BadGateway(),
            TestResponses.Success("[]"));
        var sut = CreateClient(CreateOptions(maxRetryAttempts: 3), handler);

        var result = await sut.GetMetersAsync(CancellationToken.None);

        Assert.Equal(IngestOutcome.Success, result.Outcome);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task GetMetersAsync_RateLimitedWithRetryAfter_WaitsForHeaderDurationNotDefaultBackoff()
    {
        var retryAfter = TimeSpan.FromMilliseconds(800);
        var handler = new RecordingHandler(TestResponses.RateLimited(retryAfter), TestResponses.Success("[]"));
        var sut = CreateClient(CreateOptions(maxRetryAttempts: 1), handler);

        var stopwatch = Stopwatch.StartNew();
        var result = await sut.GetMetersAsync(CancellationToken.None);
        stopwatch.Stop();

        var honouredRetryAfter = stopwatch.Elapsed >= TimeSpan.FromMilliseconds(700);
        var failureMessage = $"Expected the client to honour the {retryAfter} Retry-After header instead of the " +
            $"default ~200ms backoff, but only waited {stopwatch.Elapsed}.";

        Assert.Equal(IngestOutcome.Success, result.Outcome);
        Assert.Equal(2, handler.CallCount);
        Assert.True(honouredRetryAfter, failureMessage);
    }

    [Fact]
    public async Task GetMetersAsync_ConsecutiveFailures_OpensCircuitAndStopsReachingWeakApp()
    {
        var handler = new RecordingHandler(TestResponses.BadGateway());
        var sut = CreateClient(
            CreateOptions(maxRetryAttempts: 2, circuitBreakerMinimumThroughput: 2, circuitBreakerFailureRatio: 0.5),
            handler);

        var first = await sut.GetMetersAsync(CancellationToken.None);
        var callsAfterFirst = handler.CallCount;

        var second = await sut.GetMetersAsync(CancellationToken.None);

        Assert.Equal(IngestOutcome.HttpError, first.Outcome);
        Assert.Equal(IngestOutcome.HttpError, second.Outcome);
        Assert.Contains("circuit breaker open", second.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(callsAfterFirst, handler.CallCount);
    }

    private static WeakAppOptions CreateOptions(
        int maxRetryAttempts = 3,
        int totalTimeoutSeconds = 5,
        int attemptTimeoutSeconds = 2,
        double circuitBreakerFailureRatio = 0.5,
        int circuitBreakerMinimumThroughput = 10,
        int circuitBreakerBreakDurationSeconds = 30) =>
        new()
        {
            BaseUrl = new Uri("http://weakapp.local"),
            ApiKey = "test-api-key",
            PollingIntervalSeconds = totalTimeoutSeconds + 5,
            AttemptTimeoutSeconds = attemptTimeoutSeconds,
            TotalTimeoutSeconds = totalTimeoutSeconds,
            MaxRetryAttempts = maxRetryAttempts,
            CircuitBreakerFailureRatio = circuitBreakerFailureRatio,
            CircuitBreakerSamplingDurationSeconds = 30,
            CircuitBreakerMinimumThroughput = circuitBreakerMinimumThroughput,
            CircuitBreakerBreakDurationSeconds = circuitBreakerBreakDurationSeconds,
        };

    private static WeakAppClient CreateClient(WeakAppOptions options, RecordingHandler innerHandler)
    {
        var timeProvider = TimeProvider.System;
        var pipelineBuilder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        WeakAppResiliencePipelineFactory.Configure(pipelineBuilder, options, timeProvider);
        var pipeline = pipelineBuilder.Build();

        var resilienceHandler = new ResilienceDelegatingHandler(pipeline) { InnerHandler = innerHandler };
        var httpClient = new HttpClient(resilienceHandler) { BaseAddress = options.BaseUrl };
        return new WeakAppClient(httpClient, timeProvider, NullLogger<WeakAppClient>.Instance);
    }
}
