using Microsoft.Extensions.Options;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// What the Ingestor knows about its own polling, in memory (TASK-017): the outcome of the last
/// attempt, how many attempts failed for each reason, and the interval the loop is currently
/// running on. Deliberately not persisted — the Ingestor has no database, and this is operational
/// state about a running process, not a record of what was ingested (that is the Processor's
/// <c>ingest_batches</c>, written from the messages every attempt publishes).
/// </summary>
/// <remarks>
/// A singleton written from the polling loop and from admin requests, and read by both, so every
/// member is guarded. The interval is mutable because <c>PUT /api/v1/ingestion/config</c> can change
/// it at runtime; the bounds enforced here are the same invariant
/// <see cref="WeakAppOptionsValidator"/> enforces at startup, since an interval shorter than the
/// resilience pipeline's total budget would let retries overlap the next scheduled poll.
/// </remarks>
public sealed class IngestionRuntimeState
{
    /// <summary>
    /// Upper bound for a runtime interval change. Not a technical limit — it exists so that a typo
    /// in an admin request (an interval in milliseconds, say) fails loudly instead of silently
    /// parking ingestion for weeks.
    /// </summary>
    public const int MaxPollingIntervalSeconds = 3600;

    private readonly Lock _gate = new();
    private readonly Dictionary<IngestOutcome, int> _failureCounts = [];
    private readonly int _totalTimeoutSeconds;

    private int _pollingIntervalSeconds;
    private IngestAttemptRecorded? _lastAttempt;
    private DateTimeOffset? _lastSuccessAt;
    private int _totalPolls;

    public IngestionRuntimeState(IOptions<WeakAppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _pollingIntervalSeconds = options.Value.PollingIntervalSeconds;
        _totalTimeoutSeconds = options.Value.TotalTimeoutSeconds;
    }

    /// <summary>The interval the loop schedules its next poll on. Re-read by the loop every cycle.</summary>
    public TimeSpan PollingInterval
    {
        get
        {
            lock (_gate)
            {
                return TimeSpan.FromSeconds(_pollingIntervalSeconds);
            }
        }
    }

    /// <summary>
    /// Records a completed attempt, whether it came from the timer or from an admin trigger — both
    /// go through <see cref="StateRecordingIngestionPoller"/>, so neither path can forget to.
    /// </summary>
    public void RecordAttempt(IngestAttemptRecorded attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        lock (_gate)
        {
            _lastAttempt = attempt;
            _totalPolls++;

            if (attempt.Outcome == IngestOutcome.Success)
            {
                _lastSuccessAt = attempt.FetchedAt;
            }
            else
            {
                _failureCounts[attempt.Outcome] = _failureCounts.GetValueOrDefault(attempt.Outcome) + 1;
            }
        }
    }

    /// <summary>
    /// Applies a new polling interval, or explains why it was rejected. Returning the reason rather
    /// than throwing keeps the admin endpoint's 400 body specific about which bound was violated.
    /// </summary>
    public bool TrySetPollingInterval(int seconds, out string? error)
    {
        if (seconds <= _totalTimeoutSeconds)
        {
            error =
                $"Polling interval must be greater than the resilience pipeline's total timeout " +
                $"({_totalTimeoutSeconds}s), otherwise retries can overlap the next scheduled poll.";
            return false;
        }

        if (seconds > MaxPollingIntervalSeconds)
        {
            error = $"Polling interval must not exceed {MaxPollingIntervalSeconds}s.";
            return false;
        }

        lock (_gate)
        {
            _pollingIntervalSeconds = seconds;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// A consistent view of everything at once. Taken under the lock rather than exposed as separate
    /// properties so a status response cannot report a last outcome from one attempt alongside
    /// failure counts that already include the next one.
    /// </summary>
    public IngestionStateSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new IngestionStateSnapshot(
                _lastAttempt,
                _lastSuccessAt,
                _totalPolls,
                new Dictionary<IngestOutcome, int>(_failureCounts),
                TimeSpan.FromSeconds(_pollingIntervalSeconds));
        }
    }
}
