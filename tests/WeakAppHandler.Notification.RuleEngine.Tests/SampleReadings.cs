namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// Observation fixtures. Timestamps are offsets from a fixed instant so that cooldown assertions read
/// as "sixty seconds after the alert" rather than as literal clock values, and so nothing here
/// depends on when the suite runs.
/// </summary>
internal static class SampleReadings
{
    public static readonly DateTimeOffset Start = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid MeterA = Guid.Parse("b1000000-0000-0000-0000-00000000000a");

    public static readonly Guid MeterB = Guid.Parse("b1000000-0000-0000-0000-00000000000b");

    public static MetricObservation Numeric(
        decimal value,
        int secondsFromStart = 0,
        Guid? meterId = null,
        string metricCode = "co2",
        string location = "Kitchen",
        string meterType = "air_quality") =>
        new()
        {
            MeterId = meterId ?? MeterA,
            Location = location,
            MeterType = meterType,
            MetricCode = metricCode,
            Numeric = value,
            ObservedAt = Start.AddSeconds(secondsFromStart),
        };

    public static MetricObservation Boolean(
        bool value,
        int secondsFromStart = 0,
        Guid? meterId = null,
        string location = "Garage") =>
        new()
        {
            MeterId = meterId ?? MeterA,
            Location = location,
            MeterType = "motion",
            MetricCode = "motion_detected",
            Boolean = value,
            ObservedAt = Start.AddSeconds(secondsFromStart),
        };
}
