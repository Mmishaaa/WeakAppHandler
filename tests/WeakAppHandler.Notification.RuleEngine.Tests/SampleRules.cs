namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// Rule fixtures. Cooldown defaults to zero so that a test which is about transitions or hysteresis
/// cannot accidentally pass (or fail) because of the cooldown guard - the cooldown tests set it
/// explicitly.
/// </summary>
internal static class SampleRules
{
    /// <summary>The PRD §6.6 default hysteresis margin, in percent.</summary>
    public const decimal DefaultHysteresisPercent = 5m;

    public static readonly Guid RuleId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    public static RuleDefinition Numeric(
        RuleOperator ruleOperator,
        decimal threshold,
        decimal hysteresisPercent = DefaultHysteresisPercent,
        int cooldownSeconds = 0,
        string metricCode = "co2") =>
        new()
        {
            RuleId = RuleId,
            MetricCode = metricCode,
            Operator = ruleOperator,
            ThresholdNumeric = threshold,
            HysteresisPercent = hysteresisPercent,
            CooldownSeconds = cooldownSeconds,
        };

    public static RuleDefinition Boolean(
        bool threshold = true,
        RuleOperator ruleOperator = RuleOperator.Eq,
        decimal hysteresisPercent = 0m,
        int cooldownSeconds = 0) =>
        new()
        {
            RuleId = RuleId,
            Location = "Garage",
            MeterType = "motion",
            MetricCode = "motion_detected",
            Operator = ruleOperator,
            ThresholdBool = threshold,
            HysteresisPercent = hysteresisPercent,
            CooldownSeconds = cooldownSeconds,
        };
}
