using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// TASK-017's acceptance criteria against the running services: /status reports the real polling
/// state, /trigger polls now and answers with the batch's outcome, and every route is closed to
/// anything without a machine token carrying the <c>ingestion:admin</c> scope.
/// </summary>
[Collection(IngestionCollectionDefinition.Name)]
public sealed class IngestionAdminEndpointsTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// One meter, so a successful poll's reading count is a number the test can point at rather than
    /// the zero an empty array would produce. Shaped exactly like WeakApp's own response (camelCase,
    /// no envelope) so the real client parses it the way it parses production's.
    /// </summary>
    private const string OneMeterResponse =
        """[{"type":"air_quality","name":"Kitchen","payload":{"co2":700,"pm25":10,"humidity":40}}]""";

    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(30);

    // Queue names are fixed by the PRD, so a per-test vhost is what keeps one test's messages out of
    // another's — these tests really publish, because a triggered poll is a poll.
    private readonly string _virtualHost = $"task017-{Guid.NewGuid():N}";

    public Task InitializeAsync() => fixture.RabbitMq.CreateVirtualHostAsync(_virtualHost);

    public Task DisposeAsync() => fixture.RabbitMq.DeleteVirtualHostAsync(_virtualHost);

    [Fact]
    public async Task Status_WithoutAToken_IsRejectedWithUnauthorized()
    {
        await using var host = await StartHostAsync(TestResponses.Success(OneMeterResponse));

        using var response = await host.GetStatusAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoutes_WithAUserTokenCarryingNoScope_AreRejectedWithForbidden()
    {
        await using var host = await StartHostAsync(TestResponses.Success(OneMeterResponse));

        // The viewer's token is valid — issued by the same Auth Service, signed with the same key,
        // and accepted by authentication. It is authorization that has to reject it, because the
        // admin surface is for the Gateway's machine client and not for a signed-in human.
        using var status = await host.GetStatusAsync(host.ViewerToken);
        using var trigger = await host.TriggerAsync(host.ViewerToken);
        using var config = await host.PutConfigAsync(host.ViewerToken, pollingIntervalSeconds: 60);

        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, trigger.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, config.StatusCode);
    }

    [Fact]
    public async Task Trigger_WithAMachineToken_PollsWithinASecondAndReturnsTheBatchOutcome()
    {
        var weakApp = new RecordingHandler(TestResponses.Success(OneMeterResponse));
        await using var host = await StartHostAsync(weakApp);

        var callsBeforeTrigger = weakApp.CallCount;

        var stopwatch = Stopwatch.StartNew();
        using var response = await host.TriggerAsync(host.MachineToken);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The poll really ran for this request rather than the endpoint replaying the loop's last
        // one: WeakApp was called again, and the answer describes that call.
        Assert.True(weakApp.CallCount > callsBeforeTrigger, "The trigger did not reach WeakApp.");
        Assert.Equal(nameof(IngestOutcome.Success), body.GetProperty("outcome").GetString());
        Assert.Equal(1, body.GetProperty("readingCount").GetInt32());
        Assert.Equal(200, body.GetProperty("httpStatus").GetInt32());
        Assert.NotEqual(Guid.Empty, body.GetProperty("batchId").GetGuid());

        var failureMessage =
            $"POST /trigger answered after {stopwatch.Elapsed.TotalMilliseconds:F0}ms; the criterion is a poll " +
            "started and reported within a second.";

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), failureMessage);
    }

    [Fact]
    public async Task Status_AfterASuccessfulPoll_ReportsThatOutcomeAndTheIntervalInForce()
    {
        await using var host = await StartHostAsync(TestResponses.Success(OneMeterResponse));

        // The loop polls once at startup, so this is the state it produced on its own — /status
        // reporting on the Ingestor's real polling, not only on what a test asked for.
        var status = await host.WaitForStatusAsync(
            host.MachineToken,
            s => s.GetProperty("totalPolls").GetInt32() > 0,
            StatusTimeout,
            "the scheduled poll to be recorded");

        Assert.Equal(nameof(IngestOutcome.Success), status.GetProperty("lastOutcome").GetString());
        Assert.Equal(1, status.GetProperty("lastReadingCount").GetInt32());
        Assert.Equal(200, status.GetProperty("lastHttpStatus").GetInt32());
        Assert.Null(status.GetProperty("lastErrorMessage").GetString());
        Assert.NotEqual(Guid.Empty, status.GetProperty("lastBatchId").GetGuid());
        Assert.Empty(status.GetProperty("failureCountsByReason").EnumerateObject());
        Assert.Equal("Closed", status.GetProperty("circuitBreakerState").GetString());
        Assert.Equal(
            IngestorAdminHost.DefaultPollingIntervalSeconds,
            status.GetProperty("pollingIntervalSeconds").GetInt32());

        // Every "last..." field describes one attempt, so they have to agree with each other.
        var lastPolledAt = status.GetProperty("lastPolledAt").GetDateTimeOffset();
        Assert.Equal(lastPolledAt, status.GetProperty("lastSuccessAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Status_AfterUpstreamFailures_CountsThemByReasonAndReportsTheCircuitBreakerOpen()
    {
        // Nothing but 502s: the real resilience pipeline is still in the request path, so the breaker
        // that opens here is the one guarding production traffic, not a flag the test set.
        await using var host = await StartHostAsync(TestResponses.BadGateway());

        using var trigger = await host.TriggerAsync(host.MachineToken);
        Assert.Equal(HttpStatusCode.OK, trigger.StatusCode);

        var triggerBody = await trigger.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(nameof(IngestOutcome.HttpError), triggerBody.GetProperty("outcome").GetString());
        Assert.Equal(0, triggerBody.GetProperty("readingCount").GetInt32());

        var status = await host.WaitForStatusAsync(
            host.MachineToken,
            s => s.GetProperty("circuitBreakerState").GetString() == "Open",
            StatusTimeout,
            "the circuit breaker to open");

        Assert.Equal(nameof(IngestOutcome.HttpError), status.GetProperty("lastOutcome").GetString());
        Assert.Null(status.GetProperty("lastSuccessAt").GetString());

        // Counted by reason, not as one undifferentiated failure total: which failure mode WeakApp is
        // in is the whole point of the counters (PRD §6 F1).
        var failures = status.GetProperty("failureCountsByReason");
        Assert.True(
            failures.GetProperty(nameof(IngestOutcome.HttpError)).GetInt32() > 0,
            $"Expected HttpError failures to be counted, got: {failures}");
    }

    [Fact]
    public async Task Config_WithAValidInterval_TakesEffectAndIsReportedByStatus()
    {
        await using var host = await StartHostAsync(TestResponses.Success(OneMeterResponse));
        const int newInterval = 120;

        using var response = await host.PutConfigAsync(host.MachineToken, newInterval);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(newInterval, body.GetProperty("pollingIntervalSeconds").GetInt32());

        // The endpoint echoing the value back proves only that it parsed it; /status is where the
        // interval the loop actually schedules on is reported.
        var status = await host.ReadStatusAsync(host.MachineToken);
        Assert.Equal(newInterval, status.GetProperty("pollingIntervalSeconds").GetInt32());
    }

    [Fact]
    public async Task Config_WithAnIntervalShorterThanTheTimeoutBudget_IsRejectedAndChangesNothing()
    {
        await using var host = await StartHostAsync(TestResponses.Success(OneMeterResponse));

        // Below the pipeline's total timeout, which is the invariant WeakAppOptionsValidator enforces
        // at startup: retries of one poll would otherwise still be running when the next begins.
        using var response = await host.PutConfigAsync(host.MachineToken, pollingIntervalSeconds: 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var status = await host.ReadStatusAsync(host.MachineToken);
        Assert.Equal(
            IngestorAdminHost.DefaultPollingIntervalSeconds,
            status.GetProperty("pollingIntervalSeconds").GetInt32());
    }

    private Task<IngestorAdminHost> StartHostAsync(Func<HttpResponseMessage> weakAppResponse) =>
        StartHostAsync(new RecordingHandler(weakAppResponse));

    private Task<IngestorAdminHost> StartHostAsync(RecordingHandler weakApp) =>
        IngestorAdminHost.StartAsync(fixture, _virtualHost, weakApp);
}
