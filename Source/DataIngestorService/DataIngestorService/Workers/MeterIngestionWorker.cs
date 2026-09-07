using DataIngestorService.Clients;
using DataIngestorService.Clients.Results;
using DataIngestorService.Configuration;
using DataIngestorService.Models;
using DataIngestorService.Parsing;
using Microsoft.Extensions.Options;

namespace DataIngestorService.Workers;

sealed partial class MeterIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WeakAppOptions> options,
    ILogger<MeterIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = options.Value.PollingIntervalSeconds;

        LogPollingStarted(logger, intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            await PollSafelyAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PollSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown: the host cancelled stoppingToken.
        }

        LogPollingStopped(logger);
    }

    private async Task PollSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PollOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a failure; let it reach the loop so shutdown stays prompt.
            throw;
        }
        catch (Exception exception)
        {
            LogPollError(logger, exception);
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var client = scope.ServiceProvider.GetRequiredService<IWeakAppApiClient>();
        var result = await client.GetMetersAsync(cancellationToken);
        var durationMs = (int)result.Duration.TotalMilliseconds;

        if (result.Outcome != PollOutcome.Success)
        {
            LogPollFailed(logger, result.Outcome, result.HttpStatusCode, durationMs, result.ErrorMessage);
            return;
        }

        var readings = MeterPayloadParser.ParseAll(result.Meters);

        LogPollSucceeded(logger, durationMs, result.Meters.Count, readings.Count);

        foreach (var reading in readings)
        {
            LogReading(reading);
        }
    }

    private void LogReading(MeterReadingModel reading)
    {
        if (reading.Numeric is { } numeric)
        {
            LogNumericMetric(logger, reading.Location, reading.MeterType, reading.MetricCode, numeric);
        }
        else if (reading.Flag is { } flag)
        {
            LogBooleanMetric(logger, reading.Location, reading.MeterType, reading.MetricCode, flag);
        }
    }
}
