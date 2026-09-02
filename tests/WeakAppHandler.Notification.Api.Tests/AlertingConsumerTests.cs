using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// The path <see cref="AlertEvaluatorTests"/> cannot see: that the service really binds the
/// <c>readings.stored</c> routing key the Processor publishes to, and that what the evaluator
/// returned is actually dispatched. The evaluator-level tests would pass just as happily with the
/// consumer unregistered and the dispatcher never called.
/// </summary>
/// <remarks>
/// Each test gets its own RabbitMQ virtual host, so two tests running concurrently against the
/// shared broker cannot see each other's deliveries on queues whose names are fixed by the topology.
/// </remarks>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertingConsumerTests(IntegrationTestFixture fixture)
{
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(30);

    private static readonly DateTimeOffset Origin = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadingStoredConsumer_BreachingReading_PersistsAnAlertAndDispatchesAlertRaised()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            var metric = AlertingDatabase.NewMetricCode();
            var rule = await AddRuleAsync(
                AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m));

            await using var host = await NotificationHost.StartAsync(fixture, virtualHost);

            var meterId = Guid.NewGuid();
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 1200, Origin, location: "Kitchen"));

            var raised = await host.Dispatcher.WaitForRaisedAsync(a => a.RuleId == rule.Id, DispatchTimeout);

            Assert.Equal(meterId, raised.MeterId);
            Assert.Equal("Kitchen", raised.Location);
            Assert.Equal(metric, raised.MetricCode);
            Assert.Equal("warning", raised.Severity);
            Assert.Equal(1200, raised.TriggeredValue.Numeric);

            await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
            var alert = await reader.Alerts.AsNoTracking().SingleAsync(a => a.Id == raised.AlertId);

            Assert.Equal(AlertStatus.Active, alert.Status);
            Assert.Equal(1200m, alert.TriggeredValueNumeric);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task ReadingStoredConsumer_ValueReturningBelowTheBand_DispatchesAlertResolvedForTheSameAlert()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            var metric = AlertingDatabase.NewMetricCode();
            var rule = await AddRuleAsync(
                AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 5m));

            await using var host = await NotificationHost.StartAsync(fixture, virtualHost);

            var meterId = Guid.NewGuid();
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 1200, Origin));

            var raised = await host.Dispatcher.WaitForRaisedAsync(a => a.RuleId == rule.Id, DispatchTimeout);

            // Published only after the raise has been observed: the two readings are separate
            // messages on one queue, and a resolution evaluated before the alert exists would find
            // nothing to close.
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 900, Origin.AddSeconds(10)));

            var resolved = await host.Dispatcher.WaitForResolvedAsync(a => a.RuleId == rule.Id, DispatchTimeout);

            Assert.Equal(raised.AlertId, resolved.AlertId);
            Assert.Equal(900, resolved.ResolvedValue.Numeric);

            await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
            var alert = await reader.Alerts.AsNoTracking().SingleAsync(a => a.Id == raised.AlertId);

            Assert.Equal(AlertStatus.Resolved, alert.Status);
            Assert.Equal(Origin.AddSeconds(10), alert.ResolvedAt);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task ReadingStoredConsumer_NonBreachingReading_RaisesAndDispatchesNothing()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            var metric = AlertingDatabase.NewMetricCode();
            var rule = await AddRuleAsync(
                AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m));

            await using var host = await NotificationHost.StartAsync(fixture, virtualHost);

            var meterId = Guid.NewGuid();
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 400, Origin));

            // The evaluation state row appearing is what proves the reading was consumed at all -
            // asserting only on the absence of an alert would pass just as well if the message had
            // never been delivered. It also says the not-breaching outcome was recorded, which is
            // what makes the next reading above the threshold a transition.
            var state = await WaitForStateAsync(rule.Id, meterId);

            Assert.False(state.WasBreaching);
            Assert.Null(state.LastTriggeredAt);

            Assert.DoesNotContain(host.Dispatcher.RaisedSnapshot(), a => a.RuleId == rule.Id);

            await using var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
            Assert.False(await reader.Alerts.AnyAsync(a => a.RuleId == rule.Id));
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    /// <summary>
    /// Polls for the <c>alert_rule_state</c> row one delivery leaves behind, since a published
    /// message is consumed some time after the publish call returns.
    /// </summary>
    private async Task<AlertRuleState> WaitForStateAsync(Guid ruleId, Guid meterId)
    {
        var deadline = DateTime.UtcNow + DispatchTimeout;

        while (true)
        {
            await using (var reader = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString))
            {
                var state = await reader.AlertRuleStates.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.RuleId == ruleId && s.MeterId == meterId);

                if (state is not null)
                {
                    return state;
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"No alert_rule_state row for rule {ruleId} and meter {meterId} appeared within {DispatchTimeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    private async Task<AlertRule> AddRuleAsync(AlertRule rule)
    {
        await using var context = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString);

        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();

        return rule;
    }
}
