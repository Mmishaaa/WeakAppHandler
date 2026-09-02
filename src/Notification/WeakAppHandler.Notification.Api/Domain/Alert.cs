namespace WeakAppHandler.Notification.Api.Domain;

/// <summary>
/// One raised alert and, once the value retreats past the hysteresis band, its resolution
/// (PRD §7.1 `alerts`).
/// </summary>
/// <remarks>
/// <see cref="MeterId"/> is a plain value, not a foreign key: `meters` is owned by the Processor's
/// CoreDbContext and a cross-owner FK would couple two services' migrations to one another. That is
/// also why <see cref="Location"/>, <see cref="MeterType"/> and <see cref="MetricCode"/> are stored
/// on the row - the alert feed filters on them, and everything needed to fill them arrives on the
/// ReadingStored event, so serving the feed never needs a join into the core schema.
/// </remarks>
public sealed class Alert
{
    public required Guid Id { get; init; }

    public required Guid RuleId { get; init; }

    public required Guid MeterId { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required string MetricCode { get; init; }

    public required AlertStatus Status { get; set; }

    /// <summary>Copied from the rule when the alert is raised, so editing the rule later does not rewrite history.</summary>
    public required AlertSeverity Severity { get; init; }

    public required DateTimeOffset TriggeredAt { get; init; }

    public decimal? TriggeredValueNumeric { get; init; }

    /// <summary>
    /// Set instead of <see cref="TriggeredValueNumeric"/> for boolean metrics such as
    /// `motion_detected`. PRD §7.1 lists only a numeric column, but the seed rule set includes a
    /// boolean rule, so storing the triggering value at all requires both kinds - mirroring the
    /// value_numeric/value_bool pair the core schema already uses for readings.
    /// </summary>
    public bool? TriggeredValueBool { get; init; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public decimal? ResolvedValueNumeric { get; set; }

    public bool? ResolvedValueBool { get; set; }
}
