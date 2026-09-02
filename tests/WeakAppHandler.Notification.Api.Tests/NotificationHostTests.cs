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
    [Fact]
    public async Task StartingTheServiceTwice_ReportsReadyAndLeavesTheSeedRuleSetUntouched()
    {
        await using (var migrated = await AlertingDatabase.MigratedContextAsync(fixture.Postgres.ConnectionString))
        {
            await migrated.Database.MigrateAsync();
        }

        // The real Program.cs, twice in a row - the restart the acceptance criterion is about. The
        // alerting context has to resolve and answer the readiness probe on both starts, which is
        // what AddNotificationPersistence's AddDbContextCheck is for.
        for (var start = 0; start < 2; start++)
        {
            await using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using var context = AlertingDatabase.CreateContext(fixture.Postgres.ConnectionString);
        var seedIds = AlertRuleSeedData.All.Select(r => r.Id).ToArray();

        Assert.Equal(5, await context.AlertRules.CountAsync(r => seedIds.Contains(r.Id)));
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Notification", fixture.Postgres.ConnectionString));
}
