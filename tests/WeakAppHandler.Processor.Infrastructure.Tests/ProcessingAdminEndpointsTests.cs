using System.Net;
using System.Net.Http.Json;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// TASK-021's acceptance criteria against the running service: <c>/stats</c> is closed to anything
/// without a machine token carrying the <c>ingestion:admin</c> scope, and reports counters that
/// really moved because messages were consumed. Mirrors
/// <c>WeakAppHandler.Ingestor.Tests.IngestionAdminEndpointsTests</c>, which established this shape
/// for the Ingestor's own admin surface (TASK-017).
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class ProcessingAdminEndpointsTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(30);

    private readonly string _virtualHost = $"task021-{Guid.NewGuid():N}";

    public Task InitializeAsync() => fixture.RabbitMq.CreateVirtualHostAsync(_virtualHost);

    public Task DisposeAsync() => fixture.RabbitMq.DeleteVirtualHostAsync(_virtualHost);

    [Fact]
    public async Task Stats_WithoutAToken_IsRejectedWithUnauthorized()
    {
        await using var host = await ProcessorAdminHost.StartAsync(fixture, _virtualHost);

        using var response = await host.GetStatsAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Stats_WithAUserTokenCarryingNoScope_IsRejectedWithForbidden()
    {
        await using var host = await ProcessorAdminHost.StartAsync(fixture, _virtualHost);

        // The viewer's token is valid — issued by the same Auth Service, signed with the same key,
        // and accepted by authentication. It is authorization that has to reject it, because the
        // admin surface is for the Gateway's machine client and not for a signed-in human.
        using var response = await host.GetStatsAsync(host.ViewerToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Stats_WithAMachineToken_ReportsProcessedAndDeduplicatedCounts()
    {
        await using var host = await ProcessorAdminHost.StartAsync(fixture, _virtualHost);

        var batchId = Guid.NewGuid();
        var message = IngestionMessages.Readings(batchId, "stats-endpoint", meterCount: 1);

        // Published twice: the first delivery is a new record, the second the redelivery
        // processed_messages' idempotency check rejects.
        await host.Bus.Publish(message);
        await host.Bus.Publish(message);
        await host.Consumed.WaitForConsumeCountAsync(message.MessageId, expected: 2, ConsumeTimeout);

        var attempt = IngestionMessages.Attempt(
            Guid.NewGuid(), IngestOutcome.HttpError, readingCount: 0, httpStatus: 503, errorMessage: "boom");
        await host.Bus.Publish(attempt);
        await host.Consumed.WaitForConsumeCountAsync(attempt.MessageId, expected: 1, ConsumeTimeout);

        var stats = await host.ReadStatsAsync(host.MachineToken);

        // Two newly-recorded messages (the readings batch and the failed attempt) plus the one
        // redelivery the first publish's duplicate produced.
        Assert.Equal(2, stats.GetProperty("processed").GetInt32());
        Assert.Equal(1, stats.GetProperty("deduplicated").GetInt32());
        Assert.Equal(0, stats.GetProperty("deadLettered").GetInt32());
    }
}
