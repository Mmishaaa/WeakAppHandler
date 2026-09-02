using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Alerting;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence;
using WeakAppHandler.Notification.Api.Persistence.Configurations;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// TASK-029's own behaviour against a real PostgreSQL: what one reading writes into `alerts` and
/// `alert_rule_state`, and what the next reading then does about it. Runs the evaluator directly
/// rather than through the broker - the queue binding is <see cref="AlertingConsumerTests"/>'s
/// subject, and going through RabbitMQ for every rule-engine scenario would only make these slower
/// without covering anything more.
/// </summary>
/// <remarks>
/// Every test gives its rule a metric code no other rule uses
/// (<see cref="AlertingDatabase.NewMetricCode"/>) and a fresh meter id, so the five seed rules -
/// which apply to every location - cannot raise alerts alongside the test's own and make a count
/// assertion mean something different. The one exception is the boolean test, which is deliberately
/// about a seed rule.
/// </remarks>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertEvaluatorTests(IntegrationTestFixture fixture)
{
    private static readonly DateTimeOffset Origin = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Evaluate_ValueCrossingTheThreshold_RaisesOneActiveAlertWithTheReadingsOwnAttributes()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = await AddRuleAsync(
            context, AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m));
        var meterId = Guid.NewGuid();

        var result = await Evaluator(context).EvaluateAsync(
            StoredReadings.Numeric(meterId, metric, 1200, Origin, location: "Kitchen"),
            CancellationToken.None);

        var raised = Assert.Single(result.Raised);
        Assert.Empty(result.Resolved);

        var alert = await SingleAlertAsync(rule.Id);

        Assert.Equal(alert.Id, raised.AlertId);
        Assert.Equal(AlertStatus.Active, alert.Status);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);

        // Denormalised off the event, not joined out of the Processor's schema.
        Assert.Equal("Kitchen", alert.Location);
        Assert.Equal("air_quality", alert.MeterType);
        Assert.Equal(metric, alert.MetricCode);
        Assert.Equal(1200m, alert.TriggeredValueNumeric);

        // The observation's own instant, because that is also what the engine measures cooldown from.
        Assert.Equal(Origin, alert.TriggeredAt);
        Assert.Null(alert.ResolvedAt);
        Assert.Equal("warning", raised.Severity);

        var state = await SingleStateAsync(rule.Id, meterId);
        Assert.True(state.WasBreaching);
        Assert.Equal(Origin, state.LastTriggeredAt);
    }

    [Fact]
    public async Task Evaluate_TheSameReadingTwice_RaisesOnlyOneAlert()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = await AddRuleAsync(
            context, AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m));
        var reading = StoredReadings.Numeric(Guid.NewGuid(), metric, 1200, Origin);
        var evaluator = Evaluator(context);

        var first = await evaluator.EvaluateAsync(reading, CancellationToken.None);
        var second = await evaluator.EvaluateAsync(reading, CancellationToken.None);

        // This service keeps no processed_messages ledger of its own: a redelivery is harmless
        // because the stored breach flag makes the second evaluation a non-transition.
        Assert.Single(first.Raised);
        Assert.Empty(second.Raised);
        Assert.Equal(1, await CountAlertsAsync(rule.Id));
    }

    [Fact]
    public async Task Evaluate_BreachOnASecondMeterWithinTheFirstMetersCooldown_StillRaises()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = await AddRuleAsync(
            context,
            AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m, cooldownSeconds: 300));

        var kitchenMeter = Guid.NewGuid();
        var garageMeter = Guid.NewGuid();
        var evaluator = Evaluator(context);

        await evaluator.EvaluateAsync(
            StoredReadings.Numeric(kitchenMeter, metric, 1200, Origin, location: "Kitchen"),
            CancellationToken.None);

        // One second into the Kitchen's five-minute cooldown. A cooldown read off alert_rules
        // instead of alert_rule_state would swallow this one, which is a lost alert in another room.
        var second = await evaluator.EvaluateAsync(
            StoredReadings.Numeric(garageMeter, metric, 1300, Origin.AddSeconds(1), location: "Garage"),
            CancellationToken.None);

        Assert.Single(second.Raised);
        Assert.Equal(2, await CountAlertsAsync(rule.Id));
        Assert.Equal("Garage", (await SingleAlertAsync(rule.Id, garageMeter)).Location);
    }

    [Fact]
    public async Task Evaluate_BreachOnTheSameMeterWithinItsCooldown_DoesNotRaiseASecondAlert()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = await AddRuleAsync(
            context,
            AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m, cooldownSeconds: 300));

        var meterId = Guid.NewGuid();
        var evaluator = Evaluator(context);

        await evaluator.EvaluateAsync(
            StoredReadings.Numeric(meterId, metric, 1200, Origin), CancellationToken.None);

        // Resolved first, so what stops the second alert is the cooldown and not the open alert -
        // otherwise this test would pass just as happily with cooldown never consulted at all.
        var resolution = await evaluator.EvaluateAsync(
            StoredReadings.Numeric(meterId, metric, 900, Origin.AddSeconds(10)), CancellationToken.None);
        Assert.Single(resolution.Resolved);

        var suppressed = await evaluator.EvaluateAsync(
            StoredReadings.Numeric(meterId, metric, 1400, Origin.AddSeconds(20)), CancellationToken.None);

        Assert.Empty(suppressed.Raised);
        Assert.Equal(1, await CountAlertsAsync(rule.Id));

        // The swallowed breach is still recorded, so the next reading above the threshold is not a
        // transition either: cooldown drops the alert rather than deferring it to the window's end.
        Assert.True((await SingleStateAsync(rule.Id, meterId)).WasBreaching);
    }

    [Fact]
    public async Task Evaluate_ValueRetreatingPastTheHysteresisBand_ResolvesTheAlertWithItsValueAndInstant()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = await AddRuleAsync(
            context,
            AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 5m, cooldownSeconds: 0));

        var meterId = Guid.NewGuid();
        var evaluator = Evaluator(context);

        await evaluator.EvaluateAsync(
            StoredReadings.Numeric(meterId, metric, 1200, Origin), CancellationToken.None);

        // Below the threshold but inside the 950-1000 band: the alert stands.
        var held = await evaluator.EvaluateAsync(
            StoredReadings.Numeric(meterId, metric, 970, Origin.AddSeconds(10)), CancellationToken.None);

        Assert.Empty(held.Resolved);
        Assert.Equal(AlertStatus.Active, (await SingleAlertAsync(rule.Id)).Status);

        var clearedAt = Origin.AddSeconds(20);
        var cleared = await evaluator.EvaluateAsync(
            StoredReadings.Numeric(meterId, metric, 940, clearedAt), CancellationToken.None);

        var resolved = Assert.Single(cleared.Resolved);
        var alert = await SingleAlertAsync(rule.Id);

        Assert.Equal(alert.Id, resolved.AlertId);
        Assert.Equal(AlertStatus.Resolved, alert.Status);
        Assert.Equal(clearedAt, alert.ResolvedAt);
        Assert.Equal(940m, alert.ResolvedValueNumeric);
        Assert.False((await SingleStateAsync(rule.Id, meterId)).WasBreaching);
    }

    [Fact]
    public async Task Evaluate_AfterTheServiceIsRestarted_KeepsTheOpenAlertAndDoesNotRaiseItAgain()
    {
        var metric = AlertingDatabase.NewMetricCode();
        var meterId = Guid.NewGuid();
        AlertRule rule;

        await using (var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString))
        {
            rule = await AddRuleAsync(
                context,
                AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m, cooldownSeconds: 0));

            await Evaluator(context).EvaluateAsync(
                StoredReadings.Numeric(meterId, metric, 1200, Origin), CancellationToken.None);
        }

        // A second context and evaluator with nothing carried over in memory - the restart. The
        // engine is fed `was_breaching` and "an alert is open" from the database, so a reading whose
        // own previousValue says nothing about either still comes out as a non-transition.
        await using (var restarted = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString))
        {
            var afterRestart = await Evaluator(restarted).EvaluateAsync(
                StoredReadings.Numeric(meterId, metric, 1300, Origin.AddMinutes(30)),
                CancellationToken.None);

            Assert.Empty(afterRestart.Raised);
        }

        var alert = await SingleAlertAsync(rule.Id);
        Assert.Equal(AlertStatus.Active, alert.Status);
        Assert.Equal(1200m, alert.TriggeredValueNumeric);
    }

    [Fact]
    public async Task Evaluate_ValueCrossingTwoThresholdsOfDifferentSeverity_RaisesBothAlerts()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var warning = await AddRuleAsync(
            context,
            AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m, severity: AlertSeverity.Warning));
        var critical = await AddRuleAsync(
            context,
            AlertingDatabase.NewRule(metric, threshold: 1400m, hysteresisPercent: 0m, severity: AlertSeverity.Critical));

        var result = await Evaluator(context).EvaluateAsync(
            StoredReadings.Numeric(Guid.NewGuid(), metric, 1500, Origin), CancellationToken.None);

        // The seed set has exactly this shape (CO2 at 1000 and at 1400), so one reading really does
        // have to be able to move more than one rule.
        Assert.Equal(2, result.Raised.Count);
        Assert.Equal("warning", result.Raised.Single(a => a.RuleId == warning.Id).Severity);
        Assert.Equal("critical", result.Raised.Single(a => a.RuleId == critical.Id).Severity);
    }

    [Fact]
    public async Task Evaluate_ReadingFromAnotherLocation_RaisesNothingForALocationScopedRule()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = await AddRuleAsync(
            context,
            AlertingDatabase.NewRule(metric, threshold: 1000m, location: "Garage", hysteresisPercent: 0m));

        var result = await Evaluator(context).EvaluateAsync(
            StoredReadings.Numeric(Guid.NewGuid(), metric, 1200, Origin, location: "Kitchen"),
            CancellationToken.None);

        Assert.Empty(result.Raised);
        Assert.Equal(0, await CountAlertsAsync(rule.Id));
    }

    [Fact]
    public async Task Evaluate_BooleanReadingAgainstANumericRule_WritesNoAlertAndNoEvaluationState()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = await AddRuleAsync(context, AlertingDatabase.NewRule(metric, threshold: 1000m));
        var meterId = Guid.NewGuid();

        var result = await Evaluator(context).EvaluateAsync(
            StoredReadings.Boolean(meterId, metric, value: true, Origin), CancellationToken.None);

        Assert.Empty(result.Raised);

        // No row at all rather than a row saying "not breaching": writing a breach flag the engine
        // never computed would fake a transition on the first reading that really can be compared.
        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        Assert.False(await reader.AlertRuleStates.AnyAsync(s => s.RuleId == rule.Id && s.MeterId == meterId));
    }

    [Fact]
    public async Task Evaluate_MotionInTheGarage_RaisesAndThenResolvesTheSeedBooleanRule()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        // The real seed rule (PRD §6.6), not one this test authored: it is the only boolean rule in
        // the shipped set and the only one scoped to a location.
        var seedRule = AlertRuleSeedData.All.Single(r => r.MetricCode == "motion_detected");
        var meterId = Guid.NewGuid();
        var evaluator = Evaluator(context);

        var raised = await evaluator.EvaluateAsync(
            StoredReadings.Boolean(meterId, "motion_detected", value: true, Origin),
            CancellationToken.None);

        Assert.Equal(seedRule.Id, Assert.Single(raised.Raised).RuleId);
        Assert.True((await SingleAlertAsync(seedRule.Id, meterId)).TriggeredValueBool);

        // A boolean metric has no band to retreat through, so it clears as soon as motion stops -
        // whatever hysteresis_percent happens to say.
        var resolved = await evaluator.EvaluateAsync(
            StoredReadings.Boolean(meterId, "motion_detected", value: false, Origin.AddSeconds(30)),
            CancellationToken.None);

        Assert.Single(resolved.Resolved);

        var alert = await SingleAlertAsync(seedRule.Id, meterId);
        Assert.Equal(AlertStatus.Resolved, alert.Status);
        Assert.False(alert.ResolvedValueBool);
    }

    [Fact]
    public async Task Evaluate_DisabledRule_RaisesNothing()
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        var metric = AlertingDatabase.NewMetricCode();
        var rule = AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m);
        rule.IsEnabled = false;
        await AddRuleAsync(context, rule);

        var result = await Evaluator(context).EvaluateAsync(
            StoredReadings.Numeric(Guid.NewGuid(), metric, 1200, Origin), CancellationToken.None);

        Assert.Empty(result.Raised);
        Assert.Equal(0, await CountAlertsAsync(rule.Id));
    }

    private static AlertEvaluator Evaluator(AlertingDbContext context) =>
        new(context, NullLogger<AlertEvaluator>.Instance);

    private static async Task<AlertRule> AddRuleAsync(AlertingDbContext context, AlertRule rule)
    {
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();
        return rule;
    }

    private async Task<Alert> SingleAlertAsync(Guid ruleId, Guid? meterId = null)
    {
        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);

        return await reader.Alerts.AsNoTracking()
            .SingleAsync(a => a.RuleId == ruleId && (meterId == null || a.MeterId == meterId));
    }

    private async Task<AlertRuleState> SingleStateAsync(Guid ruleId, Guid meterId)
    {
        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);

        return await reader.AlertRuleStates.AsNoTracking()
            .SingleAsync(s => s.RuleId == ruleId && s.MeterId == meterId);
    }

    private async Task<int> CountAlertsAsync(Guid ruleId)
    {
        await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);

        return await reader.Alerts.CountAsync(a => a.RuleId == ruleId);
    }
}
