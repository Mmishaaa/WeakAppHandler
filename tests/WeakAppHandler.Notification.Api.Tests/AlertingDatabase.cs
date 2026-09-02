using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// Builds <see cref="AlertingDbContext"/> instances against the shared PostgreSQL container with the
/// same Npgsql + snake_case wiring the service configures in AddNotificationPersistence, so what the
/// tests migrate is the schema the service would create rather than a re-specified copy of it.
/// </summary>
internal static class AlertingDatabase
{
    public static AlertingDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AlertingDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AlertingDbContext(options);
    }

    public static async Task<AlertingDbContext> MigratedContextAsync(string connectionString)
    {
        var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        return context;
    }

    /// <summary>
    /// A rule of this test's own, with a fresh id, so a test that writes alerts or evaluation state
    /// never collides with another test's rows or with the seed rules whose count is asserted
    /// elsewhere.
    /// </summary>
    public static AlertRule NewRule(
        string metricCode = "co2",
        decimal threshold = 1000m,
        string? location = null,
        string? meterType = null,
        decimal hysteresisPercent = AlertRule.DefaultHysteresisPercent,
        int cooldownSeconds = AlertRule.DefaultCooldownSeconds,
        AlertSeverity severity = AlertSeverity.Warning) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"test-rule-{Guid.NewGuid():N}",
        Location = location,
        MeterType = meterType,
        MetricCode = metricCode,
        Operator = AlertOperator.Gt,
        ThresholdNumeric = threshold,
        Severity = severity,

        // Assigned rather than left to the property initialiser even when the caller took the
        // default: the two are the same value, so the store default still applies and the tests that
        // pin EF's sentinel handling down are unaffected.
        HysteresisPercent = hysteresisPercent,
        CooldownSeconds = cooldownSeconds,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// A metric code no other test and no seed rule uses, so a test's own rule is the only one a
    /// reading it publishes can match. Truncated because `metric_code` is `varchar(32)`.
    /// </summary>
    public static string NewMetricCode() => $"m{Guid.NewGuid():N}"[..17];

    /// <summary>
    /// Brings the schema up to date and lets the context go again, for callers that only need the
    /// tables to exist - the service applies no migrations of its own at startup.
    /// </summary>
    public static async Task MigrateAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
    }
}
