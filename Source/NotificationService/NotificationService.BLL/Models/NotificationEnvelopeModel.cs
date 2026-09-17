namespace NotificationService.BLL.Models;

public sealed record NotificationEnvelopeModel(
    string Group,
    IReadOnlyList<ReadingNotificationModel> Readings,
    IReadOnlyList<ReadingAlertModel> Alerts);
