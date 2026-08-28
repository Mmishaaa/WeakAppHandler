using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// The polling loop (PRD §6 F1). Runs one <see cref="IIngestionPoller.PollOnceAsync"/> per grid
/// instant produced by <see cref="PollingSchedule"/>, strictly one at a time. Because
/// <see cref="WeakAppOptionsValidator"/> already guarantees the resilience pipeline's total budget
/// is shorter than the interval, an overrun should be rare — but when it happens the missed grid
/// points are skipped rather than run back to back.
/// </summary>
internal sealed partial class IngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WeakAppOptions> options,
    TimeProvider timeProvider,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    private IngestOutcome? _lastOutcome;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);
        var scheduledTick = timeProvider.GetUtcNow();

        LogStarted(logger, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOneCycleAsync(stoppingToken).ConfigureAwait(false);

            var completedAt = timeProvider.GetUtcNow();
            scheduledTick = PollingSchedule.NextTick(scheduledTick, completedAt, interval, out var skippedCycles);

            if (skippedCycles > 0)
            {
                LogCyclesSkipped(logger, skippedCycles);
            }

            try
            {
                await Task.Delay(scheduledTick - completedAt, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingestion polling started with a {Interval} interval")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Poll cycle overran its interval; {SkippedCycles} scheduled cycle(s) skipped rather than run late")]
    private static partial void LogCyclesSkipped(ILogger logger, int skippedCycles);

    [LoggerMessage(Level = LogLevel.Error, Message = "Poll cycle failed outside the resilience pipeline")]
    private static partial void LogCycleFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "WeakApp polling recovered; ingested {ReadingCount} readings")]
    private static partial void LogPollingRecovered(ILogger logger, int readingCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WeakApp polling degraded to {Outcome} (HTTP {HttpStatus}): {ErrorMessage}")]
    private static partial void LogOutcomeDegraded(ILogger logger, IngestOutcome outcome, int? httpStatus, string? errorMessage);

    private async Task RunOneCycleAsync(CancellationToken stoppingToken)
    {
        var scope = scopeFactory.CreateAsyncScope();

        try
        {
            await using (scope.ConfigureAwait(false))
            {
                var poller = scope.ServiceProvider.GetRequiredService<IIngestionPoller>();
                var attempt = await poller.PollOnceAsync(stoppingToken).ConfigureAwait(false);
                ReportOutcome(attempt);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown, not a failure.
        }

        // The loop must survive every failure a poll can produce — a single bad cycle taking the
        // whole service down would be the opposite of resilient ingestion.
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogCycleFailed(logger, ex);
        }
    }

    /// <summary>
    /// Logs a failing outcome once per state change rather than once per attempt (PRD §6 F1): with
    /// a 10-second interval, an unreachable WeakApp would otherwise produce a warning every ten
    /// seconds for as long as it stays down, drowning out everything else in the log.
    /// </summary>
    private void ReportOutcome(IngestAttemptRecorded attempt)
    {
        if (_lastOutcome == attempt.Outcome)
        {
            return;
        }

        if (attempt.Outcome == IngestOutcome.Success)
        {
            LogPollingRecovered(logger, attempt.ReadingCount);
        }
        else
        {
            LogOutcomeDegraded(logger, attempt.Outcome, attempt.HttpStatus, attempt.ErrorMessage);
        }

        _lastOutcome = attempt.Outcome;
    }
}
