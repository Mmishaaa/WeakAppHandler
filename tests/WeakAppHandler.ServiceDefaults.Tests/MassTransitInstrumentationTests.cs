using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace WeakAppHandler.ServiceDefaults.Tests;

/// <summary>
/// TASK-044's "queue consumption rate" F10 metric: rather than a bespoke counter, this is
/// <see cref="MassTransit.InstrumentationConfigurationExtensions.UseInstrumentation"/>'s own
/// built-in "MassTransit"-named <see cref="System.Diagnostics.Metrics.Meter"/>
/// (<c>ServiceMassTransitExtensions</c> enables it on every bus this session builds), picked up by
/// <c>ServiceDefaultsExtensions</c>' <c>AddMeter("MassTransit")</c>. Proven against MassTransit's
/// in-memory test transport - no real broker needed, since the instrumentation itself is
/// transport-agnostic - through the real Prometheus scrape endpoint end to end.
/// </summary>
public sealed class MassTransitInstrumentationTests
{
    private static readonly TimeSpan HarnessTimeoutDuration = TimeSpan.FromSeconds(10);

    private static CancellationToken HarnessTimeout => new CancellationTokenSource(HarnessTimeoutDuration).Token;

    [Fact]
    public async Task MetricsEndpoint_AfterAConsumedMessage_ExposesMassTransitsOwnReceiveAndConsumeCounters()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddServiceDefaults();
        builder.Services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<ProbeConsumer>();
            cfg.UsingInMemory((context, busCfg) =>
            {
                busCfg.UseInstrumentation();
                busCfg.ConfigureEndpoints(context);
            });
        });

        await using var app = builder.Build();
        app.MapServiceDefaultsEndpoints();
        await app.StartAsync();

        var harness = app.Services.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new Probe());
        Assert.True(await harness.Consumed.Any<Probe>(HarnessTimeout));

        using var client = app.GetTestClient();
        var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("messaging_masstransit_receive", body, StringComparison.Ordinal);
        Assert.Contains("messaging_masstransit_consume", body, StringComparison.Ordinal);
    }

    private sealed record Probe;

    private sealed class ProbeConsumer : IConsumer<Probe>
    {
        public Task Consume(ConsumeContext<Probe> context) => Task.CompletedTask;
    }
}
