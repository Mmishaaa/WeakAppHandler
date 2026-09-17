using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NotificationService.API.Hubs;
using NotificationService.BLL.Models;
using NotificationService.BLL.Services;
using Shared.MessageContracts;

namespace NotificationService.API.Consumers;

sealed partial class MeterReadingsStoredConsumer(
    INotificationDispatchService notificationDispatchService,
    IHubContext<ReadingsHub, IReadingsClient> hubContext,
    ILogger<MeterReadingsStoredConsumer> logger)
    : IConsumer<MeterReadingsStored>
{
    public async Task Consume(ConsumeContext<MeterReadingsStored> context)
    {
        var message = context.Message;

        var dispatch = notificationDispatchService.Dispatch(ToNotificationModels(message));

        foreach (var envelope in dispatch.Envelopes)
        {
            var client = hubContext.Clients.Group(envelope.Group);

            await client.ReadingsReceived(envelope.Readings);

            if (envelope.Alerts.Count > 0)
            {
                await client.AlertsRaised(envelope.Alerts);
            }
        }

        LogBatchBroadcast(
            logger,
            message.BatchId,
            dispatch.ReadingCount,
            dispatch.AlertCount,
            dispatch.Envelopes.Count);
    }

    private static List<ReadingNotificationModel> ToNotificationModels(MeterReadingsStored message) =>
        [.. message.Readings.Select(reading => new ReadingNotificationModel(
            reading.Id,
            reading.MeterId,
            reading.Location,
            reading.MeterType,
            reading.MetricCode,
            reading.ObservedAt,
            reading.Numeric,
            reading.Flag))];

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Broadcast batch {BatchId}: {ReadingCount} readings, {AlertCount} alerts, {GroupCount} groups")]
    private static partial void LogBatchBroadcast(
        ILogger logger,
        Guid batchId,
        int readingCount,
        int alertCount,
        int groupCount);
}
