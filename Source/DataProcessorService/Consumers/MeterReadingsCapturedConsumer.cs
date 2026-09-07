using MassTransit;
using MessageContracts;

namespace DataProcessorService.Consumers;

sealed partial class MeterReadingsCapturedConsumer(ILogger<MeterReadingsCapturedConsumer> logger) : IConsumer<MeterReadingsCaptured>
{
    public Task Consume(ConsumeContext<MeterReadingsCaptured> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        LogBatchReceived(logger, message.BatchId, message.Readings.Count, message.CapturedAt);

        foreach (var reading in message.Readings)
        {
            LogReading(reading);
        }

        return Task.CompletedTask;
    }

    private void LogReading(MeterReadingDto reading)
    {
        if (reading.Numeric is { } numeric)
        {
            LogNumericReading(logger, reading.Location, reading.MeterType, reading.MetricCode, numeric);
        }
        else if (reading.Flag is { } flag)
        {
            LogBooleanReading(logger, reading.Location, reading.MeterType, reading.MetricCode, flag);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Received batch {BatchId}: {ReadingCount} readings captured at {CapturedAt}")]
    private static partial void LogBatchReceived(
        ILogger logger,
        Guid batchId,
        int readingCount,
        DateTimeOffset capturedAt);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "{Location} / {MeterType} / {MetricCode} = {Value}")]
    private static partial void LogNumericReading(
        ILogger logger,
        string location,
        string meterType,
        string metricCode,
        decimal value);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "{Location} / {MeterType} / {MetricCode} = {Value}")]
    private static partial void LogBooleanReading(
        ILogger logger,
        string location,
        string meterType,
        string metricCode,
        bool value);
}
