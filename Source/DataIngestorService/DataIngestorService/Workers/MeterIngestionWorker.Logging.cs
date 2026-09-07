using DataIngestorService.Clients.Results;

namespace DataIngestorService.Workers;

sealed partial class MeterIngestionWorker
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "WeakApp polling started with a {IntervalSeconds} s interval")]
    private static partial void LogPollingStarted(ILogger logger, int intervalSeconds);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "WeakApp polling stopped")]
    private static partial void LogPollingStopped(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "WeakApp poll succeeded in {DurationMs} ms: {MeterCount} meters, {ReadingCount} readings")]
    private static partial void LogPollSucceeded(ILogger logger, int durationMs, int meterCount, int readingCount);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "WeakApp poll failed with {Outcome} (HTTP {HttpStatus}) after {DurationMs} ms: {ErrorMessage}")]
    private static partial void LogPollFailed(
        ILogger logger,
        PollOutcome outcome,
        int? httpStatus,
        int durationMs,
        string? errorMessage);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "{Location} / {MeterType} / {MetricCode} = {Value}")]
    private static partial void LogNumericMetric(
        ILogger logger,
        string location,
        string meterType,
        string metricCode,
        decimal value);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "{Location} / {MeterType} / {MetricCode} = {Value}")]
    private static partial void LogBooleanMetric(
        ILogger logger,
        string location,
        string meterType,
        string metricCode,
        bool value);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "An unhandled exception occurred during WeakApp polling")]
    private static partial void LogPollError(ILogger logger, Exception exception);
}
