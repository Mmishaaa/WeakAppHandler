using WeakAppHandler.Contracts;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// ReadingStored events shaped the way the Processor publishes them (TASK-020).
/// </summary>
/// <remarks>
/// <c>previousValue</c> and <c>isChanged</c> are filled in but never asserted on by the alerting
/// tests, and that is the point: PRD §6.6's pseudocode derives the breach transition from the
/// previous value on the event, while this service derives it from `alert_rule_state` so it survives
/// a restart. A test that passed only because the event happened to carry the right previous value
/// would not notice the stored flag being ignored.
/// </remarks>
internal static class StoredReadings
{
    public static ReadingStored Numeric(
        Guid meterId,
        string metricCode,
        double value,
        DateTimeOffset observedAt,
        string location = "Kitchen",
        string meterType = "air_quality") =>
        new(meterId, location, meterType, metricCode, new MetricValue(value, null), null, true, observedAt);

    public static ReadingStored Boolean(
        Guid meterId,
        string metricCode,
        bool value,
        DateTimeOffset observedAt,
        string location = "Garage",
        string meterType = "motion") =>
        new(meterId, location, meterType, metricCode, new MetricValue(null, value), null, true, observedAt);
}
