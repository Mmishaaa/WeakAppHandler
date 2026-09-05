using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeakAppHandler.Processor.Infrastructure.Retention;

namespace WeakAppHandler.Processor.Worker.Retention;

/// <summary>
/// Runs <see cref="IRetentionJob"/> on a fixed interval (TASK-048). The admin API's
/// <c>POST /api/v1/processing/retention/run</c> endpoint runs the same job on demand, for the "run
/// it manually" test step - this loop and that endpoint share no state, so triggering one has no
/// effect on the other's own schedule.
/// </summary>
internal sealed partial class RetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<RetentionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);

        do
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<IRetentionJob>();

            try
            {
                await job.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown, not a failure.
            }

            // One bad run must not take the whole worker down - the next scheduled tick (or a
            // manual trigger) gets another chance.
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogRunFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled retention run failed")]
    private static partial void LogRunFailed(ILogger logger, Exception ex);
}
