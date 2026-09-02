using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertRuleConstraintTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task InsertRule_WithZeroCooldownZeroHysteresisAndDisabled_StoresThoseValuesNotTheColumnDefaults()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        rule.CooldownSeconds = 0;
        rule.HysteresisPercent = 0m;
        rule.IsEnabled = false;

        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        var stored = await reader.AlertRules.AsNoTracking().SingleAsync(r => r.Id == rule.Id);

        // Each of these is the CLR default for its type, which is what EF would otherwise drop from
        // the INSERT in favour of the column default - a disabled rule silently coming back enabled
        // is the worst of the three.
        Assert.Equal(0, stored.CooldownSeconds);
        Assert.Equal(0m, stored.HysteresisPercent);
        Assert.False(stored.IsEnabled);
    }

    [Fact]
    public async Task InsertRule_WithDefaultsLeftAlone_TakesThePrdDefaults()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        var stored = await reader.AlertRules.AsNoTracking().SingleAsync(r => r.Id == rule.Id);

        Assert.Equal(AlertRule.DefaultCooldownSeconds, stored.CooldownSeconds);
        Assert.Equal(AlertRule.DefaultHysteresisPercent, stored.HysteresisPercent);
        Assert.True(stored.IsEnabled);
    }

    [Fact]
    public async Task InsertRule_WithBothThresholdKindsSet_IsRejectedByTheCheckConstraint()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        rule.ThresholdBool = true;

        context.AlertRules.Add(rule);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task InsertRule_WithNegativeCooldown_IsRejectedByTheCheckConstraint()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        rule.CooldownSeconds = -1;

        context.AlertRules.Add(rule);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
