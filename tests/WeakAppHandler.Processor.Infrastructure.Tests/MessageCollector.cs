namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// Collects everything a receive endpoint delivered, so a test can wait for a specific message
/// instead of guessing at a fixed sleep.
/// </summary>
internal sealed class MessageCollector<TMessage>
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly List<TMessage> _messages = [];

    private readonly Lock _gate = new();

    public void Add(TMessage message)
    {
        lock (_gate)
        {
            _messages.Add(message);
        }
    }

    public async Task<TMessage> WaitForAsync(Func<TMessage, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            var match = Snapshot().FirstOrDefault(predicate);

            if (match is not null)
            {
                return match;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"No {typeof(TMessage).Name} matching the expected condition arrived within {timeout}.");
            }

            await Task.Delay(PollInterval).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<TMessage> Snapshot()
    {
        lock (_gate)
        {
            return [.. _messages];
        }
    }
}
