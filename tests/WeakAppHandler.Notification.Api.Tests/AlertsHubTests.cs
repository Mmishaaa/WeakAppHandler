using Microsoft.AspNetCore.SignalR.Client;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// TASK-031's acceptance criteria end to end: AlertsHub rejects an anonymous connection, and a
/// connected, authenticated client receives the same alert raised/resolved events
/// <see cref="AlertingConsumerTests"/> already proves are persisted and dispatched - here observed
/// over the real SignalR transport rather than through <see cref="RecordingAlertDispatcher"/>.
/// </summary>
/// <remarks>
/// Each test gets its own RabbitMQ virtual host, the same precedent <see cref="AlertingConsumerTests"/>
/// established, so concurrent tests cannot see each other's deliveries on queues whose names are fixed
/// by the topology.
/// </remarks>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AlertsHubTests(IntegrationTestFixture fixture)
{
    private static readonly TimeSpan SubscriptionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DuplicateGracePeriod = TimeSpan.FromSeconds(2);

    private static readonly DateTimeOffset Origin = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Connect_WithoutAccessToken_IsRejected()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            await using var host = await AlertsHubHost.StartAsync(fixture, virtualHost);
            await using var connection = host.CreateHubConnection(accessToken: null);

            // The exact exception SignalR surfaces for a rejected handshake is transport-dependent
            // (an HTTP 401 during negotiate, wrapped by the client); what TASK-031 actually requires
            // is that the connection never succeeds.
            await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task Connect_WithValidViewerToken_ReceivesAlertRaisedForABreachingReading()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            var metric = AlertingDatabase.NewMetricCode();
            var rule = await AddRuleAsync(
                AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m));

            await using var host = await AlertsHubHost.StartAsync(fixture, virtualHost);
            await using var connection = host.CreateHubConnection(host.ViewerToken);

            var firstRaised = new TaskCompletionSource<AlertRaised>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<AlertRaised>("AlertRaised", alert => firstRaised.TrySetResult(alert));

            await connection.StartAsync();

            var meterId = Guid.NewGuid();
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 1200, Origin, location: "Kitchen"));

            var raised = await firstRaised.Task.WaitAsync(SubscriptionTimeout);

            Assert.Equal(rule.Id, raised.RuleId);
            Assert.Equal(meterId, raised.MeterId);
            Assert.Equal("Kitchen", raised.Location);
            Assert.Equal(metric, raised.MetricCode);
            Assert.Equal(1200, raised.TriggeredValue.Numeric);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task Connect_WithValidViewerToken_ReceivesAlertResolvedForTheSameAlert()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            var metric = AlertingDatabase.NewMetricCode();
            var rule = await AddRuleAsync(
                AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 5m));

            await using var host = await AlertsHubHost.StartAsync(fixture, virtualHost);
            await using var connection = host.CreateHubConnection(host.ViewerToken);

            var firstRaised = new TaskCompletionSource<AlertRaised>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstResolved = new TaskCompletionSource<AlertResolved>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<AlertRaised>("AlertRaised", alert => firstRaised.TrySetResult(alert));
            connection.On<AlertResolved>("AlertResolved", alert => firstResolved.TrySetResult(alert));

            await connection.StartAsync();

            var meterId = Guid.NewGuid();
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 1200, Origin));

            var raised = await firstRaised.Task.WaitAsync(SubscriptionTimeout);

            // Published only after the raise has been observed: the two readings are separate
            // messages on one queue, and a resolution evaluated before the alert exists would find
            // nothing to close.
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 900, Origin.AddSeconds(10)));

            var resolved = await firstResolved.Task.WaitAsync(SubscriptionTimeout);

            Assert.Equal(raised.AlertId, resolved.AlertId);
            Assert.Equal(900, resolved.ResolvedValue.Numeric);
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }
    }

    [Fact]
    public async Task Connect_WithValidViewerToken_ReceivesEachAlertExactlyOnce()
    {
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            var metric = AlertingDatabase.NewMetricCode();
            await AddRuleAsync(AlertingDatabase.NewRule(metric, threshold: 1000m, hysteresisPercent: 0m));

            await using var host = await AlertsHubHost.StartAsync(fixture, virtualHost);
            await using var connection = host.CreateHubConnection(host.ViewerToken);

            var received = new List<AlertRaised>();
            var firstRaised = new TaskCompletionSource<AlertRaised>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<AlertRaised>("AlertRaised", alert =>
            {
                lock (received)
                {
                    received.Add(alert);
                }

                firstRaised.TrySetResult(alert);
            });

            await connection.StartAsync();

            var meterId = Guid.NewGuid();
            await host.Bus.Publish(StoredReadings.Numeric(meterId, metric, 1200, Origin));

            await firstRaised.Task.WaitAsync(SubscriptionTimeout);

            // One commit dispatches once (IAlertDispatcher's own contract); this is the window a
            // duplicate broadcast would have to show up in.
            await Task.Delay(DuplicateGracePeriod);

            lock (received)
            {
                Assert.Single(received);
            }
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
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
