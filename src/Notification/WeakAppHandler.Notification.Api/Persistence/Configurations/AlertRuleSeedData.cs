using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Persistence.Configurations;

/// <summary>
/// The default rule set from PRD §6.6, applied through EF's HasData so it lands exactly once - in
/// the migration that creates the table. That is what makes it idempotent across restarts without a
/// startup "does this rule already exist" scan, and it also means an operator who deletes a seed
/// rule through the TASK-030 REST surface keeps it deleted instead of having it silently reappear.
/// </summary>
/// <remarks>
/// Identifiers and timestamps are fixed literals rather than generated values: HasData is evaluated
/// when a migration is scaffolded, so anything non-deterministic would show up as a spurious
/// data change in the next migration. Metric codes are the normalised ones the Processor writes
/// (`motion_detected`, not WeakApp's wire name `motionDetected`) - see PayloadNormalizer. Location
/// and meter type are left null ("any") except where the rule itself is scoped to one room; the
/// metric code already implies the meter type.
/// </remarks>
public static class AlertRuleSeedData
{
    private static readonly DateTimeOffset SeedCreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<AlertRule> All { get; } =
    [
        new AlertRule
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
            Name = "CO2 above 1000 ppm",
            Location = null,
            MeterType = null,
            MetricCode = "co2",
            Operator = AlertOperator.Gt,
            ThresholdNumeric = 1000m,
            Severity = AlertSeverity.Warning,
            CreatedAt = SeedCreatedAt,
            UpdatedAt = SeedCreatedAt,
        },
        new AlertRule
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
            Name = "CO2 above 1400 ppm",
            Location = null,
            MeterType = null,
            MetricCode = "co2",
            Operator = AlertOperator.Gt,
            ThresholdNumeric = 1400m,
            Severity = AlertSeverity.Critical,
            CreatedAt = SeedCreatedAt,
            UpdatedAt = SeedCreatedAt,
        },
        new AlertRule
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
            Name = "PM2.5 above 35 ug/m3",
            Location = null,
            MeterType = null,
            MetricCode = "pm25",
            Operator = AlertOperator.Gt,
            ThresholdNumeric = 35m,
            Severity = AlertSeverity.Warning,
            CreatedAt = SeedCreatedAt,
            UpdatedAt = SeedCreatedAt,
        },
        new AlertRule
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"),
            Name = "Humidity above 70%",
            Location = null,
            MeterType = null,
            MetricCode = "humidity",
            Operator = AlertOperator.Gt,
            ThresholdNumeric = 70m,
            Severity = AlertSeverity.Info,
            CreatedAt = SeedCreatedAt,
            UpdatedAt = SeedCreatedAt,
        },
        new AlertRule
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"),
            Name = "Motion detected in Garage",
            Location = "Garage",
            MeterType = "motion",
            MetricCode = "motion_detected",
            Operator = AlertOperator.Eq,
            ThresholdBool = true,

            // A boolean metric has no band to retreat through, so a non-zero hysteresis margin would
            // only be a number nobody can act on; resolution for this rule is the value going false.
            HysteresisPercent = 0m,
            Severity = AlertSeverity.Warning,
            CreatedAt = SeedCreatedAt,
            UpdatedAt = SeedCreatedAt,
        },
    ];
}
