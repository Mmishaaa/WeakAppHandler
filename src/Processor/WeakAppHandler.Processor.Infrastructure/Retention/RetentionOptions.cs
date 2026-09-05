namespace WeakAppHandler.Processor.Infrastructure.Retention;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>
    /// How far back raw <c>readings</c> rows are kept before being rolled up into
    /// <c>readings_hourly</c> and deleted (TASK-048). Configurable via
    /// <c>Retention__WindowDays</c> without a code change.
    /// </summary>
    public int WindowDays { get; set; } = 30;

    /// <summary>How often the background job runs.</summary>
    public int IntervalMinutes { get; set; } = 60;

    public TimeSpan Window => TimeSpan.FromDays(WindowDays);

    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);
}
