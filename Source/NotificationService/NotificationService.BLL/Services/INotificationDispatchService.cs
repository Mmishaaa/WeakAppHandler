using NotificationService.BLL.Models;

namespace NotificationService.BLL.Services;

public interface INotificationDispatchService
{
    NotificationDispatchModel Dispatch(IReadOnlyList<ReadingNotificationModel> readings);
}
