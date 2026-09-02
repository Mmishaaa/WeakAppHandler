using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Notification.Api.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertingSchemaTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_CreatesTheThreeAlertingTables()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var tables = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT table_name AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name IN ('alert_rules', 'alerts', 'alert_rule_state')
                """)
            .ToListAsync();

        Assert.Equal(
            ["alert_rule_state", "alert_rules", "alerts"],
            tables.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_LeavesAlertsWithoutAForeignKeyIntoProcessorTables()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        // Whatever `alerts` points at is what couples this service's migrations to another owner's
        // schema, so the assertion is on the referenced tables themselves rather than on a count.
        var referencedTables = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT DISTINCT ccu.table_name AS "Value"
                FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu
                  ON ccu.constraint_name = tc.constraint_name
                 AND ccu.constraint_schema = tc.constraint_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_schema = 'public'
                  AND tc.table_name IN ('alerts', 'alert_rule_state')
                """)
            .ToListAsync();

        Assert.Equal(["alert_rules"], referencedTables);
    }

    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_CreatesTheActiveAlertPartialIndex()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var definition = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT indexdef AS "Value"
                FROM pg_indexes
                WHERE tablename = 'alerts'
                  AND indexname = 'ux_alerts_active_rule_meter_metric'
                """)
            .SingleAsync();

        Assert.Contains("UNIQUE INDEX", definition, StringComparison.Ordinal);
        Assert.Contains("WHERE", definition, StringComparison.Ordinal);
        Assert.Contains("'active'", definition, StringComparison.Ordinal);
    }
}
