using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence.Configurations;

namespace WeakAppHandler.Notification.Api.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertRuleSeedTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_SeedsThePrdRuleSet()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var seedIds = AlertRuleSeedData.All.Select(r => r.Id).ToArray();
        var seeded = await context.AlertRules
            .Where(r => seedIds.Contains(r.Id))
            .OrderBy(r => r.Id)
            .ToListAsync();

        Assert.Equal(5, seeded.Count);
        Assert.All(seeded, rule => Assert.True(rule.IsEnabled));

        // PRD §6.6's table, read off the database rather than off the seed constants, so a rule that
        // failed to reach the column it was meant to reach shows up here.
        Assert.Equal(
            [
                ("co2", AlertOperator.Gt, 1000m, AlertSeverity.Warning),
                ("co2", AlertOperator.Gt, 1400m, AlertSeverity.Critical),
                ("pm25", AlertOperator.Gt, 35m, AlertSeverity.Warning),
                ("humidity", AlertOperator.Gt, 70m, AlertSeverity.Info),
            ],
            seeded
                .Where(r => r.ThresholdNumeric is not null)
                .Select(r => (r.MetricCode, r.Operator, r.ThresholdNumeric!.Value, r.Severity)));
    }

    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_SeedsTheGarageMotionRuleAsABooleanRule()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var motionRule = await context.AlertRules.SingleAsync(r => r.MetricCode == "motion_detected");

        Assert.Equal("Garage", motionRule.Location);
        Assert.Equal(AlertOperator.Eq, motionRule.Operator);
        Assert.True(motionRule.ThresholdBool);
        Assert.Null(motionRule.ThresholdNumeric);

        // A boolean rule is seeded with no hysteresis band. EF omits a HasData value equal to the CLR
        // default when the column carries a store default, which would have silently turned this into
        // the 5.00 default - the migration writes the column explicitly to prevent exactly that.
        Assert.Equal(0m, motionRule.HysteresisPercent);
    }

    [Fact]
    public async Task Migrate_RunASecondTime_DoesNotDuplicateTheSeedRules()
    {
        await using var first = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);
        await using var second = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var seedIds = AlertRuleSeedData.All.Select(r => r.Id).ToArray();
        var seededCount = await second.AlertRules.CountAsync(r => seedIds.Contains(r.Id));
        var seedNameCount = await second.AlertRules
            .CountAsync(r => r.Name == "CO2 above 1000 ppm");

        Assert.Equal(5, seededCount);

        // Counting by id alone cannot see a duplicate, since a second copy would need a new id; the
        // name is what a duplicated seed would repeat.
        Assert.Equal(1, seedNameCount);
    }
}
