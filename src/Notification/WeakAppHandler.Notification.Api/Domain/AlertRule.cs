namespace WeakAppHandler.Notification.Api.Domain;

/// <summary>
/// A threshold rule (PRD §6.6, §7.1 `alert_rules`): a match on (location, meter type, metric) plus a
/// comparison against a threshold, with the hysteresis and cooldown parameters that keep a single
/// breach from producing an alert storm.
/// </summary>
/// <remarks>
/// <see cref="Location"/> and <see cref="MeterType"/> are nullable because null means "any" - §6.6's
/// rule model allows a rule to apply across every location while the seed motion rule is scoped to
/// one. A rule owns no navigation into the Processor's `meters`/`metrics` tables: those belong to a
/// different service, so <see cref="MetricCode"/> is matched by value off the ReadingStored event.
/// </remarks>
public sealed class AlertRule
{
    /// <summary>Default hysteresis margin from PRD §6.6 (5%).</summary>
    public const decimal DefaultHysteresisPercent = 5.00m;

    /// <summary>Default cooldown from PRD §6.6 (300 seconds).</summary>
    public const int DefaultCooldownSeconds = 300;

    public required Guid Id { get; init; }

    public required string Name { get; set; }

    public string? Location { get; set; }

    public string? MeterType { get; set; }

    public required string MetricCode { get; set; }

    public required AlertOperator Operator { get; set; }

    public decimal? ThresholdNumeric { get; set; }

    public bool? ThresholdBool { get; set; }

    public required AlertSeverity Severity { get; set; }

    public decimal HysteresisPercent { get; set; } = DefaultHysteresisPercent;

    public int CooldownSeconds { get; set; } = DefaultCooldownSeconds;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Last time this rule fired for any meter. Kept because PRD §7.1 lists it and it is what an
    /// operator looks at in a rule list, but it is deliberately NOT what cooldown is computed from -
    /// that is per (rule, meter, metric) in <see cref="AlertRuleState"/>, so a breach in one room
    /// cannot suppress one in another.
    /// </summary>
    public DateTimeOffset? LastTriggeredAt { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; set; }
}
