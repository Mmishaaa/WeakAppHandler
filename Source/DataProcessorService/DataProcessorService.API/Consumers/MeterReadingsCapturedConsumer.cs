using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Services;
using MassTransit;
using Shared.MessageContracts;
using Shared.Telemetry;

namespace DataProcessorService.API.Consumers;

internal sealed partial class MeterReadingsCapturedConsumer(
    IReadingBatchService readingBatchService,
    IngestionMetrics metrics,
    ILogger<MeterReadingsCapturedConsumer> logger)
    : IConsumer<MeterReadingsCaptured>
{
    public async Task Consume(ConsumeContext<MeterReadingsCaptured> context)
    {
        var message = context.Message;

        var messageId = context.MessageId ?? message.BatchId;

        using var batchScope = Serilog.Context.LogContext.PushProperty("BatchId", message.BatchId);
        using var messageScope = Serilog.Context.LogContext.PushProperty("MessageId", messageId);

        var result = await readingBatchService.WriteAsync(
            ToBatchModel(message),
            context.CancellationToken);

        if (!result.IsSuccess)
        {
            LogBatchRejected(logger, message.BatchId, result.Error.Code, result.Error.Message);
            return;
        }

        var write = result.Value;

        await context.Publish(ToStoredEvent(message.BatchId, write), context.CancellationToken);

        metrics.ReadingsStored(write.Readings.Count);
        LogBatchStored(logger, message.BatchId, write.Readings.Count);
    }

    private static MeterReadingsBatchModel ToBatchModel(MeterReadingsCaptured message) =>
        new(
            message.BatchId,
            message.CapturedAt,
            [.. message.Readings.Select(ToReadingModel)]);

    private static MeterReadingModel ToReadingModel(MeterReadingDto reading) =>
        new(reading.Location, reading.MeterType, reading.MetricCode, reading.Numeric, reading.Flag);

    private static MeterReadingsStored ToStoredEvent(Guid batchId, BatchWriteModel write) =>
        new(
            batchId,
            DateTimeOffset.UtcNow,
            [.. write.Readings.Select(ToStoredDto)]);

    private static StoredMeterReadingDto ToStoredDto(StoredReadingModel reading) =>
        new(
            reading.Id,
            reading.MeterId,
            reading.Location,
            reading.MeterType,
            reading.MetricCode,
            reading.ObservedAt,
            reading.Numeric,
            reading.Flag);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Stored batch {BatchId}: {ReadingCount} readings")]
    private static partial void LogBatchStored(ILogger logger, Guid batchId, int readingCount);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Batch {BatchId} rejected [{ErrorCode}]: {ErrorMessage}")]
    private static partial void LogBatchRejected(
        ILogger logger,
        Guid batchId,
        string errorCode,
        string errorMessage);
}
