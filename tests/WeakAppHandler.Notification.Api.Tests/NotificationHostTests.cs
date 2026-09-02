using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Notification.Api.Persistence.Configurations;

namespace WeakAppHandler.Notification.Api.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class NotificationHostTests(IntegrationTestFixture fixture)
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReadyPollInterval = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task StartingTheServiceTwice_ReportsReadyAndLeavesTheSeedRuleSetUntouched()
    {
        await using (var migrated = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString))
        {
            await migrated.Database.MigrateAsync();
        }

        // Its own virtual host, so the queues this host declares are not the ones a concurrently
        // running consumer test is asserting on.
        var virtualHost = $"vh-{Guid.NewGuid():N}";
        await fixture.RabbitMq.CreateVirtualHostAsync(virtualHost);

        try
        {
            // The real Program.cs, twice in a row - the restart the acceptance criterion is about.
            // Both the alerting context and the message bus have to resolve and answer the readiness
            // probe on each start; MassTransit contributes its own "ready"-tagged health check, so
            // the bus is part of what readiness now means for this service.
            for (var start = 0; start < 2; start++)
            {
                await using var factory = CreateFactory(virtualHost);
                using var client = factory.CreateClient();

                await AssertBecomesReadyAsync(client);
            }
        }
        finally
        {
            await fixture.RabbitMq.DeleteVirtualHostAsync(virtualHost);
        }

        await using var context = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        var seedIds = AlertRuleSeedData.All.Select(r => r.Id).ToArray();

        Assert.Equal(5, await context.AlertRules.CountAsync(r => seedIds.Contains(r.Id)));
    }

    /// <summary>
    /// Waits for the readiness probe to report healthy, rather than demanding it on the first call.
    /// </summary>
    /// <remarks>
    /// Readiness is eventually consistent here, and deliberately so: MassTransit connects in the
    /// background, which TASK-012 recorded as a decision worth keeping - a service that refuses to
    /// start because the broker is briefly unreachable is a worse failure than one that reports
    /// itself unready for a moment and then recovers. So a 503 immediately after startup is the
    /// service behaving correctly, and what has to be asserted is that it becomes ready, which is
    /// exactly what an orchestrator's probe does. Revisited in TASK-047 with compose ordering.
    /// </remarks>
    private static async Task AssertBecomesReadyAsync(HttpClient client)
    {
        var deadline = DateTime.UtcNow + ReadyTimeout;

        while (true)
        {
            using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                // The body names the component that is unhealthy, which is the difference between
                // "the bus was still connecting" and a real regression in a different check.
                var body = await response.Content.ReadAsStringAsync();
                Assert.Fail($"/health/ready never reported healthy within {ReadyTimeout}. Last body: {body}");
            }

            await Task.Delay(ReadyPollInterval);
        }
    }

    private WebApplicationFactory<Program> CreateFactory(string virtualHost)
    {
        var amqp = new Uri(fixture.RabbitMq.ConnectionString);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Notification", fixture.Postgres.ConnectionString)
            .UseSetting("RabbitMq:Host", amqp.Host)
            .UseSetting("RabbitMq:Port", amqp.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:VirtualHost", virtualHost)
            .UseSetting("RabbitMq:Username", RabbitMqIntegrationFixture.Username)
            .UseSetting("RabbitMq:Password", RabbitMqIntegrationFixture.Password));
    }
}
