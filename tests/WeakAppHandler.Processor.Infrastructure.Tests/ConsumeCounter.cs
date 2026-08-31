using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// Counts how often the bus finished consuming each message id. A duplicate-delivery test needs to
/// know that the redelivery was actually handled before it can claim the database is unchanged;
/// without that signal the assertion would only prove the second message had not been processed
/// yet.
/// </summary>
internal sealed class ConsumeCounter : IConsumeObserver
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly Dictionary<Guid, int> _consumed = [];

    private readonly Lock _gate = new();

    public Task PreConsume<T>(ConsumeContext<T> context)
        where T : class => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        if (MessageIdOf(context.Message) is { } messageId)
        {
            lock (_gate)
            {
                _consumed[messageId] = _consumed.TryGetValue(messageId, out var count) ? count + 1 : 1;
            }
        }

        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception)
        where T : class => Task.CompletedTask;

    public async Task WaitForConsumeCountAsync(Guid messageId, int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (CountOf(messageId) < expected)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Message {messageId} was consumed {CountOf(messageId)} times within {timeout}, expected {expected}.");
            }

            await Task.Delay(PollInterval).ConfigureAwait(false);
        }
    }

    private static Guid? MessageIdOf(object message) => message switch
    {
        Contracts.ReadingsIngested readings => readings.MessageId,
        Contracts.IngestAttemptRecorded attempt => attempt.MessageId,
        _ => null,
    };

    private int CountOf(Guid messageId)
    {
        lock (_gate)
        {
            return _consumed.TryGetValue(messageId, out var count) ? count : 0;
        }
    }
}
