using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Ingestor.WeakApp;

/// <summary>
/// Classifies the result of one <c>GET /meters</c> call against the failure modes observed and
/// documented in docs/weakapp-observed-response.json and PRD §3.3. Retry/backoff/circuit-breaker
/// behaviour lives entirely in the resilience handler attached to the injected <see cref="HttpClient"/>
/// (see <see cref="WeakAppResiliencePipelineFactory"/>); this class only turns the handler's final
/// outcome - a response, or an exception it gave up on - into an <see cref="IngestOutcome"/>.
/// </summary>
public sealed partial class WeakAppClient(HttpClient httpClient, TimeProvider timeProvider, ILogger<WeakAppClient> logger)
    : IWeakAppClient
{
    private const string MetersPath = "/meters";

    public async Task<WeakAppFetchResult> GetMetersAsync(CancellationToken cancellationToken)
    {
        var start = timeProvider.GetTimestamp();

        try
        {
            using var response = await httpClient
                .GetAsync(MetersPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return await ClassifyResponseAsync(response, start, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutRejectedException ex)
        {
            LogTimeoutBudgetExceeded(logger, ex);
            return Failure(IngestOutcome.Timeout, httpStatusCode: null, ex.Message, timeProvider.GetElapsedTime(start));
        }
        catch (BrokenCircuitException ex)
        {
            LogCircuitBreakerOpen(logger, ex);
            return Failure(IngestOutcome.HttpError, httpStatusCode: null, $"Circuit breaker open: {ex.Message}", timeProvider.GetElapsedTime(start));
        }
        catch (HttpRequestException ex)
        {
            LogNetworkFailure(logger, ex);
            return Failure(IngestOutcome.HttpError, httpStatusCode: null, ex.Message, timeProvider.GetElapsedTime(start));
        }
    }

    private static WeakAppFetchResult Failure(IngestOutcome outcome, int? httpStatusCode, string errorMessage, TimeSpan duration) =>
        new()
        {
            Outcome = outcome,
            HttpStatusCode = httpStatusCode,
            ErrorMessage = errorMessage,
            Duration = duration,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "WeakApp poll exceeded its resilience timeout budget")]
    private static partial void LogTimeoutBudgetExceeded(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WeakApp poll rejected: circuit breaker is open")]
    private static partial void LogCircuitBreakerOpen(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WeakApp poll failed with a network-level error")]
    private static partial void LogNetworkFailure(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WeakApp response body could not be read/parsed")]
    private static partial void LogResponseBodyUnreadable(ILogger logger, Exception ex);

    private async Task<WeakAppFetchResult> ClassifyResponseAsync(HttpResponseMessage response, long start, CancellationToken cancellationToken)
    {
        var duration = timeProvider.GetElapsedTime(start);
        var statusCode = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Failure(IngestOutcome.Unauthorized, statusCode, "Invalid or missing API key", duration);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Failure(IngestOutcome.RateLimited, statusCode, "Rate limit exceeded", duration);
        }

        if (!response.IsSuccessStatusCode)
        {
            return Failure(IngestOutcome.HttpError, statusCode, $"WeakApp returned HTTP {statusCode}", duration);
        }

        List<WeakAppMeterDto>? meters;
        try
        {
            meters = await response.Content.ReadFromJsonAsync<List<WeakAppMeterDto>>(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or HttpRequestException)
        {
            // Observed live: the connection can be reset mid-body-read ("Error while copying content
            // to a stream"). Treated as the real-world corrupted-payload case per PRD §3.3, since the
            // documented {"error":"data corrupted"} 200 body was never actually reproduced.
            LogResponseBodyUnreadable(logger, ex);
            return Failure(IngestOutcome.Corrupted, statusCode, ex.Message, duration);
        }

        if (meters is null)
        {
            return Failure(IngestOutcome.Corrupted, statusCode, "Response body was empty", duration);
        }

        // An empty-but-well-formed array is a legitimate zero-reading success, not `corrupted`.
        return new WeakAppFetchResult
        {
            Outcome = IngestOutcome.Success,
            HttpStatusCode = statusCode,
            Meters = meters,
            Duration = duration,
        };
    }
}
