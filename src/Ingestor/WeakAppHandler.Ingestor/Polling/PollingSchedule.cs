namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// Places polls on a fixed grid of instants spaced one interval apart. Kept as a pure function so
/// the overrun rule PRD §6 F1 states — "skip a cycle rather than queueing overlapping work" — is
/// decidable without a clock, a broker or a running host.
/// </summary>
internal static class PollingSchedule
{
    /// <summary>
    /// Returns the first grid instant strictly after <paramref name="completedAt"/>, given the grid
    /// point the cycle that just finished was scheduled for. When a cycle overruns, the grid points
    /// it ran past are reported through <paramref name="skippedCycles"/> and are NOT run late: the
    /// alternative — catching up — would issue polls back to back with no gap, which is precisely
    /// the overlap the interval exists to prevent.
    /// </summary>
    public static DateTimeOffset NextTick(
        DateTimeOffset scheduledTick,
        DateTimeOffset completedAt,
        TimeSpan interval,
        out int skippedCycles)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        skippedCycles = 0;
        var next = scheduledTick + interval;

        while (next <= completedAt)
        {
            next += interval;
            skippedCycles++;
        }

        return next;
    }
}
