namespace NotificationService.BLL.Models;

public sealed record ReadingAlertModel(
    ReadingNotificationModel Reading,
    AlertKind Kind,
    decimal Threshold);
