using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NotificationService.API.Hubs;
using NotificationService.BLL.Models;
using NotificationService.BLL.Notifications;
using NotificationService.BLL.Services;
using Shared.MessageContracts;
using Shared.Telemetry;

namespace NotificationService.API.Consumers;

sealed partial class MeterReadingsStoredConsumer(
    INotificationDispatchService notificationDispatchService,
    IHubContext<ReadingsHub, IReadingsClient> hubContext,
    IngestionMetrics metrics,
    ILogger<MeterReadingsStoredConsumer> logger)
    : IConsumer<MeterReadingsStored>
{
    public async Task Consume(ConsumeContext<MeterReadingsStored> context)
    {
        var message = context.Message;

        using var batchScope = Serilog.Context.LogContext.PushProperty("BatchId", message.BatchId);

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

        RecordAlerts(dispatch);

        LogBatchBroadcast(
            logger,
            message.BatchId,
            dispatch.ReadingCount,
            dispatch.AlertCount,
            dispatch.Envelopes.Count);
    }

    private void RecordAlerts(NotificationDispatchModel dispatch)
    {
        var raised = dispatch.Envelopes
            .FirstOrDefault(envelope => envelope.Group == NotificationGroups.All)?.Alerts;

        if (raised is null)
        {
            return;
        }

        foreach (var group in raised.GroupBy(alert => alert.Kind))
        {
            metrics.AlertsRaised(group.Key.ToString(), group.Count());
        }
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
