using System.Diagnostics;
using DataIngestorService.Clients;
using DataIngestorService.Clients.Results;
using DataIngestorService.Configuration;
using DataIngestorService.Models;
using DataIngestorService.Parsing;
using MassTransit;
using Microsoft.Extensions.Options;
using Shared.MessageContracts;
using Shared.Telemetry;

namespace DataIngestorService.Workers;

internal sealed partial class MeterIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WeakAppOptions> options,
    IngestionMetrics metrics,
    ILogger<MeterIngestionWorker> logger) : BackgroundService
{
    private const string PollActivityName = "weakapp.poll";
    private const string OutcomeTag = "weakapp.poll.outcome";
    private const string MeterCountTag = "weakapp.poll.meters";
    private const string ReadingCountTag = "weakapp.poll.readings";
    private const string BatchIdTag = "weakapphandler.batch_id";

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
        }

        LogPollingStopped(logger);
    }

    private static MeterReadingDto ToDto(MeterReadingModel reading) =>
        new(reading.Location, reading.MeterType, reading.MetricCode, reading.Numeric, reading.Flag);

    private async Task PollSafelyAsync(CancellationToken cancellationToken)
    {
        using var activity = IngestionTracing.Source.StartActivity(PollActivityName);

        try
        {
            await PollOnceAsync(activity, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            LogPollError(logger, exception);
        }
    }

    private async Task PollOnceAsync(Activity? activity, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var client = scope.ServiceProvider.GetRequiredService<IWeakAppApiClient>();
        var result = await client.GetMetersAsync(cancellationToken);
        var durationMs = (int)result.Duration.TotalMilliseconds;

        activity?.SetTag(OutcomeTag, result.Outcome.ToString());

        if (result.Outcome != PollOutcome.Success)
        {
            activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
            LogPollFailed(logger, result.Outcome, result.HttpStatusCode, durationMs, result.ErrorMessage);
            return;
        }

        var readings = MeterPayloadParser.ParseAll(result.Meters);

        activity?.SetTag(MeterCountTag, result.Meters.Count);
        activity?.SetTag(ReadingCountTag, readings.Count);

        if (readings.Count == 0)
        {
            LogPollEmpty(logger, durationMs, result.Meters.Count);
            return;
        }

        LogPollSucceeded(logger, durationMs, result.Meters.Count, readings.Count);

        foreach (var reading in readings)
        {
            LogReading(reading);
        }

        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        await PublishAsync(activity, publishEndpoint, readings, cancellationToken);
    }

    private async Task PublishAsync(
        Activity? activity,
        IPublishEndpoint publishEndpoint,
        IReadOnlyList<MeterReadingModel> readings,
        CancellationToken cancellationToken)
    {
        var batchId = NewId.NextGuid();

        activity?.SetTag(BatchIdTag, batchId);

        using var batchScope = Serilog.Context.LogContext.PushProperty("BatchId", batchId);

        var message = new MeterReadingsCaptured(
            batchId,
            DateTimeOffset.UtcNow,
            [.. readings.Select(ToDto)]);

        await publishEndpoint.Publish(message, cancellationToken);

        foreach (var group in readings.GroupBy(reading => reading.MeterType, StringComparer.Ordinal))
        {
            metrics.ReadingsIngested(group.Key, group.Count());
        }

        LogBatchPublished(logger, batchId, message.Readings.Count);
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
