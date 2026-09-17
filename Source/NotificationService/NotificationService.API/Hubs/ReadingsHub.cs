using Microsoft.AspNetCore.SignalR;
using NotificationService.BLL.Notifications;

namespace NotificationService.API.Hubs;

public sealed class ReadingsHub : Hub<IReadingsClient>
{
    private const string SubscriptionKey = "readings.subscription";

    public async Task<string> Subscribe(string? location, string? metricCode)
    {
        await LeaveCurrentGroupAsync();

        var group = NotificationGroups.For(location, metricCode);

        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        Context.Items[SubscriptionKey] = group;

        return group;
    }

    public async Task Unsubscribe() => await LeaveCurrentGroupAsync();

    private async Task LeaveCurrentGroupAsync()
    {
        if (Context.Items.Remove(SubscriptionKey, out var current) && current is string group)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        }
    }
}
