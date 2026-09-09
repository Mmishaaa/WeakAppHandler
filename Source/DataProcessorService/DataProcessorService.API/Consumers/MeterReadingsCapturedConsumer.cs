using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Results;
using DataProcessorService.BLL.Services;
using MassTransit;
using MessageContracts;

namespace DataProcessorService.API.Consumers;

sealed partial class MeterReadingsCapturedConsumer(
    IReadingBatchService readingBatchService,
    ILogger<MeterReadingsCapturedConsumer> logger)
    : IConsumer<MeterReadingsCaptured>
{
    public async Task Consume(ConsumeContext<MeterReadingsCaptured> context)
    {
        var message = context.Message;

        var messageId = context.MessageId ?? message.BatchId;

        var result = await readingBatchService.WriteAsync(
            ToBatchModel(messageId, message),
            context.CancellationToken);

        if (!result.IsSuccess)
        {
            LogBatchRejected(logger, message.BatchId, result.Error.Code, result.Error.Message);
            return;
        }

        if (result.Value == BatchWriteResult.Duplicate)
        {
            LogBatchDuplicate(logger, message.BatchId, messageId);
            return;
        }

        LogBatchStored(logger, message.BatchId, message.Readings.Count);
    }

    private static MeterReadingsBatchModel ToBatchModel(Guid messageId, MeterReadingsCaptured message) =>
        new(
            messageId,
            message.BatchId,
            message.CapturedAt,
            [.. message.Readings.Select(reading => new MeterReadingModel(
                reading.Location,
                reading.MeterType,
                reading.MetricCode,
                reading.Numeric,
                reading.Flag))]);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Stored batch {BatchId}: {ReadingCount} readings")]
    private static partial void LogBatchStored(ILogger logger, Guid batchId, int readingCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Batch {BatchId} was already processed (message {MessageId}), nothing written")]
    private static partial void LogBatchDuplicate(ILogger logger, Guid batchId, Guid messageId);

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
