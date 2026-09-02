using WeakAppHandler.Contracts;
using WeakAppHandler.Notification.Api.Alerting;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// Stands in for the SignalR hub that will consume these events (TASK-031), recording what the
/// consumer dispatched so a test can tell "the alert was written" apart from "the alert was
/// announced".
/// </summary>
internal sealed class RecordingAlertDispatcher : IAlertDispatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly List<AlertRaised> _raised = [];
    private readonly List<AlertResolved> _resolved = [];
    private readonly Lock _gate = new();

    public Task DispatchRaisedAsync(AlertRaised alert, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _raised.Add(alert);
        }

        return Task.CompletedTask;
    }

    public Task DispatchResolvedAsync(AlertResolved alert, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _resolved.Add(alert);
        }

        return Task.CompletedTask;
    }

    public Task<AlertRaised> WaitForRaisedAsync(Func<AlertRaised, bool> predicate, TimeSpan timeout) =>
        WaitForAsync(_raised, predicate, timeout);

    public Task<AlertResolved> WaitForResolvedAsync(Func<AlertResolved, bool> predicate, TimeSpan timeout) =>
        WaitForAsync(_resolved, predicate, timeout);

    public IReadOnlyList<AlertRaised> RaisedSnapshot()
    {
        lock (_gate)
        {
            return [.. _raised];
        }
    }

    private async Task<T> WaitForAsync<T>(List<T> source, Func<T, bool> predicate, TimeSpan timeout)
        where T : class
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            T? match;

            lock (_gate)
            {
                match = source.FirstOrDefault(predicate);
            }

            if (match is not null)
            {
                return match;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"No {typeof(T).Name} matching the expected condition was dispatched within {timeout}.");
            }

            await Task.Delay(PollInterval).ConfigureAwait(false);
        }
    }
}
