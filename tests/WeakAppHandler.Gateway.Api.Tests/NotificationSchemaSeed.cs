extern alias NotificationApi;

using Microsoft.EntityFrameworkCore;
using NotificationApi::WeakAppHandler.Notification.Api.Domain;
using NotificationApi::WeakAppHandler.Notification.Api.Persistence;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// Seeds the alerting schema through Notification's own <c>AlertingDbContext</c> - the service that
/// owns and migrates it - so these tests exercise the Gateway against exactly the tables/columns
/// production writes, not a hand-rolled schema that could silently drift from it.
/// </summary>
/// <remarks>
/// Every member here takes and returns only plain types (<see cref="Guid"/>, <see cref="string"/>),
/// never the aliased <c>AlertRule</c>/<c>AlertStatus</c>/etc. types themselves - the
/// <c>extern alias</c> the csproj comment explains (needed only to avoid a <c>Program</c> type
/// collision with Notification.Api) stays confined to this one file rather than spreading the
/// ceremony to every test that seeds a rule or an alert.
/// </remarks>
internal static class NotificationSchemaSeed
{
    public static async Task<AlertingDbContext> CreateMigratedContextAsync(string connectionString)
    {
        var context = new AlertingDbContext(
            new DbContextOptionsBuilder<AlertingDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options);

        await context.Database.MigrateAsync();

        return context;
    }

    /// <summary>
    /// A rule of this test's own, with a fresh id and name, so a test's assertions are never
    /// confused by the five seed rules TASK-027's migration always applies alongside it.
    /// </summary>
    public static async Task<Guid> AddRuleAsync(
        AlertingDbContext context,
        string metricCode,
        string? location = null,
        string? meterType = null,
        string severity = "warning")
    {
        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            Name = $"gateway-test-rule-{Guid.NewGuid():N}",
            Location = location,
            MeterType = meterType,
            MetricCode = metricCode,
            Operator = AlertOperator.Gt,
            ThresholdNumeric = 1000m,
            Severity = ParseSeverity(severity),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        return rule.Id;
    }

    /// <summary>A metric code no seed rule uses, so a test's own rule/alerts are never joined by seed-rule noise.</summary>
    public static string NewMetricCode() => $"m{Guid.NewGuid():N}"[..17];

    /// <summary>
    /// An alert for <paramref name="ruleId"/>. <paramref name="resolvedAt"/> left null keeps it
    /// active; the check constraint on `alerts` requires resolved_at to be non-null if and only if
    /// <paramref name="status"/> is <c>"resolved"</c>, so passing that status without a resolvedAt
    /// (or vice versa) fails at the database, not silently.
    /// </summary>
    public static async Task<Guid> AddAlertAsync(
        AlertingDbContext context,
        Guid ruleId,
        string metricCode,
        Guid meterId,
        string location,
        string meterType,
        DateTimeOffset triggeredAt,
        decimal triggeredValueNumeric,
        string severity = "warning",
        string status = "active",
        DateTimeOffset? resolvedAt = null,
        decimal? resolvedValueNumeric = null)
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            RuleId = ruleId,
            MeterId = meterId,
            Location = location,
            MeterType = meterType,
            MetricCode = metricCode,
            Status = ParseStatus(status),
            Severity = ParseSeverity(severity),
            TriggeredAt = triggeredAt,
            TriggeredValueNumeric = triggeredValueNumeric,
            ResolvedAt = resolvedAt,
            ResolvedValueNumeric = resolvedValueNumeric,
        };

        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        return alert.Id;
    }

    private static AlertSeverity ParseSeverity(string severity) => severity switch
    {
        "info" => AlertSeverity.Info,
        "warning" => AlertSeverity.Warning,
        "critical" => AlertSeverity.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown alert severity."),
    };

    private static AlertStatus ParseStatus(string status) => status switch
    {
        "active" => AlertStatus.Active,
        "resolved" => AlertStatus.Resolved,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown alert status."),
    };
}
