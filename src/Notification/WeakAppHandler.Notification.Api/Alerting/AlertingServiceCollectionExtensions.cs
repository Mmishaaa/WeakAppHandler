using Microsoft.Extensions.DependencyInjection.Extensions;
using WeakAppHandler.Notification.Api.Telemetry;

namespace WeakAppHandler.Notification.Api.Alerting;

public static class AlertingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the rule evaluation path. The dispatcher is a TryAdd so a host - or TASK-031's
    /// SignalR hub - can register its own beforehand and keep this default out of the way.
    /// </summary>
    public static IServiceCollection AddAlerting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped: it holds the AlertingDbContext for one message, and MassTransit gives each
        // delivery its own scope.
        services.AddScoped<AlertEvaluator>();
        services.TryAddSingleton<IAlertDispatcher, LoggingAlertDispatcher>();
        services.TryAddSingleton<NotificationMetrics>();

        return services;
    }
}
