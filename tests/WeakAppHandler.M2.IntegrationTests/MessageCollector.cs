namespace WeakAppHandler.M2.IntegrationTests;

/// <summary>
/// Collects everything a receive endpoint delivered, so a test can wait for the specific message it
/// cares about instead of for a fixed sleep.
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

    public IReadOnlyList<TMessage> Snapshot()
    {
        lock (_gate)
        {
            return [.. _messages];
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
}
