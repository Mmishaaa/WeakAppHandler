namespace WeakAppHandler.Notification.Api.Admin;

/// <summary>
/// The wire shape for creating or replacing an <see cref="Domain.AlertRule"/> (TASK-030). Operator
/// and severity are the lower-case codes <see cref="Persistence.Converters.AlertOperatorConverter"/>/
/// <see cref="Persistence.Converters.AlertSeverityConverter"/> already use for the column itself, so
/// the REST contract and the database spelling never diverge. <c>HysteresisPercent</c>/
/// <c>CooldownSeconds</c>/<c>IsEnabled</c> are nullable: omitting them means "use the entity's own
/// default" (5%/300s/enabled), the same defaults a hand-written INSERT would get from the database.
/// </summary>
public sealed record AlertRuleRequest(
    string Name,
    string? Location,
    string? MeterType,
    string MetricCode,
    string Operator,
    decimal? ThresholdNumeric,
    bool? ThresholdBool,
    string Severity,
    decimal? HysteresisPercent,
    int? CooldownSeconds,
    bool? IsEnabled);
