using System.Net;
using DataIngestorService.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace DataIngestorService.Clients;

static class WeakAppResiliencePipeline
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(200);

    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, WeakAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));

        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = BaseDelay,
            ShouldRetryAfterHeader = true,
            ShouldHandle = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
        });

        builder.AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or IOException or TimeoutRejectedException)
        {
            return true;
        }

        if (outcome.Result is not { } response)
        {
            return false;
        }

        return (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;
    }
}
