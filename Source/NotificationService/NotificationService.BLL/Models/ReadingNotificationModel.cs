namespace NotificationService.BLL.Models;

public sealed record ReadingNotificationModel(
    long Id,
    Guid MeterId,
    string Location,
    string MeterType,
    string MetricCode,
    DateTimeOffset ObservedAt,
    decimal? Numeric,
    bool? Flag);
