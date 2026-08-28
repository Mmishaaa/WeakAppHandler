namespace WeakAppHandler.Messaging.Tests;

/// <summary>
/// Counts how many times the always-failing consumer was handed the message. Dead-lettering is only
/// meaningful once the retry policy is actually exhausted, so the test waits on this rather than on
/// a fixed sleep that would pass just as happily if MassTransit gave up on the first attempt.
/// </summary>
internal sealed class ConsumeAttemptCounter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Record() => Interlocked.Increment(ref _count);

    public async Task WaitForAsync(int expectedAttempts, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (Count < expectedAttempts)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"The consumer was invoked {Count} times within {timeout}, expected {expectedAttempts}.");
            }

            await Task.Delay(PollInterval);
        }
    }
}
