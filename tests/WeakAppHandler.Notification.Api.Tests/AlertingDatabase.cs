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
    public static AlertRule NewRule(string metricCode = "co2", decimal threshold = 1000m) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"test-rule-{Guid.NewGuid():N}",
        MetricCode = metricCode,
        Operator = AlertOperator.Gt,
        ThresholdNumeric = threshold,
        Severity = AlertSeverity.Warning,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
