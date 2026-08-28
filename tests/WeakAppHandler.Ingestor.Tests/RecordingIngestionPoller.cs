using System.Diagnostics;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.Polling;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// Records when each poll started and how many ran at once, so the worker's scheduling can be
/// judged on observable behaviour instead of on log output.
/// </summary>
internal sealed class RecordingIngestionPoller(TimeSpan pollDuration, int throwOnFirstCalls = 0) : IIngestionPoller
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private readonly List<TimeSpan> _startOffsets = [];

    private readonly Lock _gate = new();

    private int _concurrentPolls;

    public int CallCount { get; private set; }

    public int MaxConcurrentPolls { get; private set; }

    public IReadOnlyList<TimeSpan> StartOffsets
    {
        get
        {
            lock (_gate)
            {
                return [.. _startOffsets];
            }
        }
    }

    public async Task<IngestAttemptRecorded> PollOnceAsync(CancellationToken cancellationToken)
    {
        int callNumber;

        lock (_gate)
        {
            _startOffsets.Add(_clock.Elapsed);
            CallCount++;
            callNumber = CallCount;
            _concurrentPolls++;
            MaxConcurrentPolls = Math.Max(MaxConcurrentPolls, _concurrentPolls);
        }

        try
        {
            await Task.Delay(pollDuration, cancellationToken).ConfigureAwait(false);

            if (callNumber <= throwOnFirstCalls)
            {
                throw new InvalidOperationException($"Simulated failure on poll {callNumber}.");
            }

            return new IngestAttemptRecorded(
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                IngestOutcome.Success,
                200,
                (int)pollDuration.TotalMilliseconds,
                0,
                null);
        }
        finally
        {
            lock (_gate)
            {
                _concurrentPolls--;
            }
        }
    }

    public async Task WaitForCallsAsync(int callCount, TimeSpan timeout)
    {
        var deadline = _clock.Elapsed + timeout;

        while (CallCount < callCount)
        {
            if (_clock.Elapsed > deadline)
            {
                throw new TimeoutException($"Only {CallCount} of {callCount} polls started within {timeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }
    }
}
