using WeakAppHandler.Ingestor.Polling;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// The overrun rule from PRD §6 F1, decided without a clock: a cycle that runs past its successors
/// skips them instead of running them late back to back.
/// </summary>
public sealed class PollingScheduleTests
{
    private static readonly DateTimeOffset Tick = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    [Fact]
    public void NextTick_CycleFinishesInsideItsInterval_SchedulesTheVeryNextGridPoint()
    {
        var next = PollingSchedule.NextTick(Tick, Tick.AddSeconds(4), Interval, out var skipped);

        Assert.Equal(Tick.AddSeconds(10), next);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void NextTick_CycleOverrunsTwoIntervals_SkipsThemInsteadOfRunningThemLate()
    {
        var next = PollingSchedule.NextTick(Tick, Tick.AddSeconds(25), Interval, out var skipped);

        // 12:00:10 and 12:00:20 passed while the cycle was still running. Catching up would issue
        // two polls back to back with no gap — exactly the overlap the interval exists to prevent.
        Assert.Equal(Tick.AddSeconds(30), next);
        Assert.Equal(2, skipped);
    }

    [Fact]
    public void NextTick_CycleFinishesExactlyOnAGridPoint_TreatsThatPointAsMissed()
    {
        // A cycle that ends at 12:00:10.000 has consumed that grid point; starting a poll at the
        // same instant it finished is a back-to-back run, not a scheduled one.
        var next = PollingSchedule.NextTick(Tick, Tick.AddSeconds(10), Interval, out var skipped);

        Assert.Equal(Tick.AddSeconds(20), next);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void NextTick_ScheduleIsAbsolute_SoDelaysDoNotAccumulateDrift()
    {
        var current = Tick;

        // Every cycle finishes 1.5s late; after ten of them the schedule is still on the original
        // grid rather than fifteen seconds behind it.
        for (var cycle = 0; cycle < 10; cycle++)
        {
            current = PollingSchedule.NextTick(current, current.AddSeconds(1.5), Interval, out _);
        }

        Assert.Equal(Tick.AddSeconds(100), current);
    }

    [Fact]
    public void NextTick_NonPositiveInterval_IsRejectedRatherThanLoopingForever()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PollingSchedule.NextTick(Tick, Tick, TimeSpan.Zero, out _));
    }
}
