using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataIngestorService.Clients.Results;
using DataIngestorService.Contracts;

namespace DataIngestorService.Clients;

sealed class WeakAppApiClient(HttpClient httpClient) : IWeakAppApiClient
{
    private static readonly Uri MetersPath = new("/meters", UriKind.Relative);
    private const int MaxErrorMessageLength = 1024;

    public async Task<MeterFetchResult> GetMetersAsync(CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.GetAsync(MetersPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return await ClassifyAsync(response, start, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(PollOutcome.Timeout, httpStatusCode: null, "The request timed out", start);
        }
        catch (HttpRequestException exception)
        {
            return Failure(PollOutcome.HttpError, (int?)exception.StatusCode, exception.Message, start);
        }
    }

    private static async Task<MeterFetchResult> ClassifyAsync(
        HttpResponseMessage response,
        long start,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                Failure(PollOutcome.Unauthorized, statusCode, "Invalid or missing API key", start),

            HttpStatusCode.TooManyRequests =>
                Failure(PollOutcome.RateLimited, statusCode, "Rate limit exceeded", start),

            _ when !response.IsSuccessStatusCode =>
                Failure(PollOutcome.HttpError, statusCode, $"WeakApp returned HTTP {statusCode}", start),

            _ => await ParseSuccessResponseAsync(response, statusCode, start, cancellationToken),
        };
    }

    private static async Task<MeterFetchResult> ParseSuccessResponseAsync(
        HttpResponseMessage response,
        int statusCode,
        long start,
        CancellationToken cancellationToken)
    {
        List<WeakAppMeterDto>? meters;

        try
        {
            meters = await response.Content.ReadFromJsonAsync(
                WeakAppJsonContext.Default.ListWeakAppMeterDto,
                cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or IOException or HttpRequestException)
        {
            return Failure(PollOutcome.Corrupted, statusCode, exception.Message, start);
        }

        if (meters is null)
        {
            return Failure(PollOutcome.Corrupted, statusCode, "Response body was empty", start);
        }

        return new MeterFetchResult
        {
            Outcome = PollOutcome.Success,
            HttpStatusCode = statusCode,
            Meters = meters,
            Duration = Stopwatch.GetElapsedTime(start),
        };
    }

    private static MeterFetchResult Failure(
        PollOutcome outcome,
        int? httpStatusCode,
        string errorMessage,
        long start) =>
        new()
        {
            Outcome = outcome,
            HttpStatusCode = httpStatusCode,
            ErrorMessage = Truncate(errorMessage),
            Duration = Stopwatch.GetElapsedTime(start),
        };

    private static string Truncate(string value) =>
        value.Length <= MaxErrorMessageLength ? value : value[..MaxErrorMessageLength];
}
