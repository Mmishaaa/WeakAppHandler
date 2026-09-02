using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertPersistenceTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task InsertAlert_ForAMeterThatExistsOnlyInTheProcessorSchema_StoresLocationMeterTypeAndMetricCode()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        // A meter id this database has never seen: nothing in the alerting schema may require it to
        // resolve, because `meters` belongs to the Processor.
        var meterId = Guid.NewGuid();
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            MeterId = meterId,
            Location = "Kitchen",
            MeterType = "air_quality",
            MetricCode = "co2",
            Status = AlertStatus.Active,
            Severity = AlertSeverity.Warning,
            TriggeredAt = DateTimeOffset.UtcNow,
            TriggeredValueNumeric = 1234m,
        };

        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        var stored = await reader.Alerts.AsNoTracking().SingleAsync(a => a.Id == alert.Id);

        Assert.Equal("Kitchen", stored.Location);
        Assert.Equal("air_quality", stored.MeterType);
        Assert.Equal("co2", stored.MetricCode);
        Assert.Equal(AlertStatus.Active, stored.Status);
        Assert.Equal(1234m, stored.TriggeredValueNumeric);
    }

    [Fact]
    public async Task InsertSecondActiveAlert_ForTheSameRuleMeterAndMetric_IsRejectedByThePartialUniqueIndex()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        var meterId = Guid.NewGuid();
        context.Alerts.Add(NewAlert(rule.Id, meterId, AlertStatus.Active));
        await context.SaveChangesAsync();

        await using var second = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        second.Alerts.Add(NewAlert(rule.Id, meterId, AlertStatus.Active));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task InsertActiveAlert_AfterTheEarlierOneResolved_IsAllowed()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        var meterId = Guid.NewGuid();
        var resolved = NewAlert(rule.Id, meterId, AlertStatus.Resolved);
        resolved.ResolvedAt = DateTimeOffset.UtcNow;
        resolved.ResolvedValueNumeric = 400m;
        context.Alerts.Add(resolved);
        context.Alerts.Add(NewAlert(rule.Id, meterId, AlertStatus.Active));

        await context.SaveChangesAsync();

        var count = await context.Alerts.CountAsync(a => a.RuleId == rule.Id && a.MeterId == meterId);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AlertRuleState_ForTwoMetersUnderOneRule_AreSeparateRows()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var rule = AlertingDatabase.NewRule();
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        var kitchenMeter = Guid.NewGuid();
        var garageMeter = Guid.NewGuid();
        var triggeredAt = DateTimeOffset.UtcNow;

        context.AlertRuleStates.Add(new AlertRuleState
        {
            RuleId = rule.Id,
            MeterId = kitchenMeter,
            MetricCode = "co2",
            WasBreaching = true,
            LastTriggeredAt = triggeredAt,
            LastEvaluatedAt = triggeredAt,
        });
        context.AlertRuleStates.Add(new AlertRuleState
        {
            RuleId = rule.Id,
            MeterId = garageMeter,
            MetricCode = "co2",
            WasBreaching = false,
            LastEvaluatedAt = triggeredAt,
        });

        await context.SaveChangesAsync();

        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        var states = await reader.AlertRuleStates.AsNoTracking()
            .Where(s => s.RuleId == rule.Id)
            .ToListAsync();

        // Cooldown lives on these rows, so one rule holding two independent cooldowns is the whole
        // point: a breach in the Kitchen must not silence one in the Garage.
        Assert.Equal(2, states.Count);
        Assert.Null(states.Single(s => s.MeterId == garageMeter).LastTriggeredAt);
        Assert.NotNull(states.Single(s => s.MeterId == kitchenMeter).LastTriggeredAt);
    }

    private static Alert NewAlert(Guid ruleId, Guid meterId, AlertStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RuleId = ruleId,
        MeterId = meterId,
        Location = "Garage",
        MeterType = "air_quality",
        MetricCode = "co2",
        Status = status,
        Severity = AlertSeverity.Warning,
        TriggeredAt = DateTimeOffset.UtcNow,
        TriggeredValueNumeric = 1500m,
    };
}
