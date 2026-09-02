namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>
/// Everything the engine needs to know about one rule: its scope, its comparison, and the two
/// storm-suppression parameters. A pure projection of `alert_rules` with the persistence concerns
/// (name, timestamps, `last_triggered_at`) left out.
/// </summary>
/// <remarks>
/// <see cref="ThresholdNumeric"/> is a decimal to mirror `numeric(12,4)` exactly - the caller
/// converts the double carried on ReadingStored once, at the boundary, rather than the engine
/// comparing a binary float against a decimal threshold and disagreeing with the database about what
/// "exactly at the threshold" means.
/// </remarks>
public sealed record RuleDefinition
{
    public required Guid RuleId { get; init; }

    /// <summary>Location this rule is scoped to; null means any location (PRD §6.6).</summary>
    public string? Location { get; init; }

    /// <summary>Meter type this rule is scoped to; null means any meter type.</summary>
    public string? MeterType { get; init; }

    /// <summary>Normalised metric code, as the Processor writes it (`motion_detected`, not `motionDetected`).</summary>
    public required string MetricCode { get; init; }

    public required RuleOperator Operator { get; init; }

    /// <summary>Threshold for a numeric rule. Exactly one of this and <see cref="ThresholdBool"/> is set.</summary>
    public decimal? ThresholdNumeric { get; init; }

    /// <summary>Threshold for a boolean rule. Exactly one of this and <see cref="ThresholdNumeric"/> is set.</summary>
    public bool? ThresholdBool { get; init; }

    /// <summary>
    /// Percentage of the threshold's magnitude a value must retreat past the threshold before an
    /// active alert clears (PRD §6.6, default 5). Zero means "clears as soon as it stops breaching".
    /// Ignored for boolean rules, which have no band to retreat through.
    /// </summary>
    public decimal HysteresisPercent { get; init; }

    /// <summary>Minimum interval between two alerts for the same (rule, meter, metric). Negative values are treated as zero.</summary>
    public int CooldownSeconds { get; init; }

    /// <summary>
    /// Whether the rule participates in evaluation at all. Callers are expected to filter on this in
    /// their query; <see cref="AlertRuleEngine.Matches"/> also honours it so a disabled rule that
    /// reaches the engine by mistake still cannot raise anything.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
