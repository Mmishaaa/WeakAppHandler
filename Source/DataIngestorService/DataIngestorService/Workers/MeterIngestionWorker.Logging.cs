using DataIngestorService.Clients.Results;

namespace DataIngestorService.Workers;

internal sealed partial class MeterIngestionWorker
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
        Level = LogLevel.Debug,
        Message = "{Location} / {MeterType} / {MetricCode} = {Value}")]
    private static partial void LogNumericMetric(
        ILogger logger,
        string location,
        string meterType,
        string metricCode,
        decimal value);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Debug,
        Message = "{Location} / {MeterType} / {MetricCode} = {Value}")]
    private static partial void LogBooleanMetric(
        ILogger logger,
        string location,
        string meterType,
        string metricCode,
        bool value);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Published batch {BatchId} with {ReadingCount} readings")]
    private static partial void LogBatchPublished(ILogger logger, Guid batchId, int readingCount);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "An unhandled exception occurred during WeakApp polling")]
    private static partial void LogPollError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "WeakApp poll succeeded in {DurationMs} ms but yielded no usable readings from {MeterCount} meters")]
    private static partial void LogPollEmpty(ILogger logger, int durationMs, int meterCount);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Error,
        Message = "Batch {BatchId} with {ReadingCount} readings could not be published and was dropped")]
    private static partial void LogBatchDropped(ILogger logger, Guid batchId, int readingCount, Exception exception);
}
