using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence.Converters;

namespace WeakAppHandler.Notification.Api.Admin;

/// <summary>The wire shape of one <see cref="Domain.AlertRule"/> (TASK-030).</summary>
public sealed record AlertRuleResponse(
    Guid Id,
    string Name,
    string? Location,
    string? MeterType,
    string MetricCode,
    string Operator,
    decimal? ThresholdNumeric,
    bool? ThresholdBool,
    string Severity,
    decimal HysteresisPercent,
    int CooldownSeconds,
    bool IsEnabled,
    DateTimeOffset? LastTriggeredAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Maps a persisted <see cref="AlertRule"/> onto its wire shape, translating the operator
    /// and severity enums back to the lower-case codes the request side accepts.</summary>
    public static AlertRuleResponse FromEntity(AlertRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new AlertRuleResponse(
            rule.Id,
            rule.Name,
            rule.Location,
            rule.MeterType,
            rule.MetricCode,
            AlertOperatorConverter.ToCode(rule.Operator),
            rule.ThresholdNumeric,
            rule.ThresholdBool,
            AlertSeverityConverter.ToCode(rule.Severity),
            rule.HysteresisPercent,
            rule.CooldownSeconds,
            rule.IsEnabled,
            rule.LastTriggeredAt,
            rule.CreatedAt,
            rule.UpdatedAt);
    }
}
