using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.Ingestor.Polling;

public static class IngestionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the polling loop and the poller it drives. The poller is scoped rather than
    /// singleton because <see cref="MassTransit.IPublishEndpoint"/> is: the loop opens a scope per
    /// cycle, and TASK-017's admin trigger will resolve the same poller from its request scope.
    /// </summary>
    public static IHostApplicationBuilder AddIngestionPolling(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddScoped<IIngestionPoller, IngestionPoller>();
        builder.Services.AddHostedService<IngestionWorker>();

        return builder;
    }
}
