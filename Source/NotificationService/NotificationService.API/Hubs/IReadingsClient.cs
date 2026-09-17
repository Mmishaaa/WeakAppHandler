using NotificationService.BLL.Models;

namespace NotificationService.API.Hubs;

public interface IReadingsClient
{
    Task ReadingsReceived(IReadOnlyList<ReadingNotificationModel> readings);

    Task AlertsRaised(IReadOnlyList<ReadingAlertModel> alerts);
}
