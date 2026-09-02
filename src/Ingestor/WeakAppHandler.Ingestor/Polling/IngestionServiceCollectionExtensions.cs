using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.Ingestor.Polling;

public static class IngestionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the polling loop, the poller it drives, and the in-memory state the admin API
    /// (TASK-017) reports on. The poller is scoped rather than singleton because
    /// <see cref="MassTransit.IPublishEndpoint"/> is: the loop opens a scope per cycle, and the
    /// admin trigger resolves the same poller from its request scope. The state is a singleton for
    /// the opposite reason — it has to outlive both.
    /// </summary>
    public static IHostApplicationBuilder AddIngestionPolling(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<IngestionRuntimeState>();

        // The concrete poller is registered separately from the interface so the decorator can take
        // it as a dependency; anything resolving IIngestionPoller gets the recording one, which is
        // what makes "every attempt is observable in /status" true by construction rather than by
        // every call site remembering to record.
        builder.Services.TryAddScoped<IngestionPoller>();
        builder.Services.TryAddScoped<IIngestionPoller>(provider => new StateRecordingIngestionPoller(
            provider.GetRequiredService<IngestionPoller>(),
            provider.GetRequiredService<IngestionRuntimeState>()));

        builder.Services.AddHostedService<IngestionWorker>();

        return builder;
    }
}
