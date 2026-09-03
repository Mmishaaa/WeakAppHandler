using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.Polling;
using WeakAppHandler.Ingestor.WeakApp;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Messaging;

namespace WeakAppHandler.M2.IntegrationTests;

/// <summary>
/// The real Ingestor exactly as Program.cs wires it — <c>AddServiceMassTransit</c> with no consumers
/// of its own (the Ingestor only ever publishes) plus <c>AddIngestionPolling</c> — against the same
/// broker and virtual host a <see cref="ProcessorEndToEndHost"/> is started on, with only the HTTP
/// call to WeakApp faked. Unlike WeakAppHandler.Ingestor.Tests' own IngestorHost, the collector
/// consumers here are bound to their own capture-only queue names rather than the production
/// readings.ingested/readings.attempt names: the Processor's real consumers are the ones bound to
/// those, and a topic exchange delivers an independent copy to every distinct bound queue, so this
/// capture never competes with the real processing for a message. It exists purely so a test can
/// observe the batch id a real poll produced and, for the duplicate-delivery scenario, redeliver the
/// exact message the Processor already consumed once.
/// </summary>
internal sealed class IngestorEndToEndHost(
    IHost host,
    MessageCollector<ReadingsIngested> ingested,
    MessageCollector<IngestAttemptRecorded> attempts) : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    public MessageCollector<ReadingsIngested> Ingested => ingested;

    public MessageCollector<IngestAttemptRecorded> Attempts => attempts;

    public static async Task<IngestorEndToEndHost> StartAsync(
        RabbitMqIntegrationFixture fixture,
        string virtualHost,
        IWeakAppClient weakAppClient,
        int pollingIntervalSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var amqp = new Uri(fixture.ConnectionString);
        var ingested = new MessageCollector<ReadingsIngested>();
        var attempts = new MessageCollector<IngestAttemptRecorded>();

        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = amqp.Host,
            ["RabbitMq:Port"] = amqp.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:VirtualHost"] = virtualHost,
            ["RabbitMq:Username"] = RabbitMqIntegrationFixture.Username,
            ["RabbitMq:Password"] = RabbitMqIntegrationFixture.Password,

            // The first poll runs immediately on startup; a long interval just keeps a second cycle
            // from starting mid-assertion. BaseUrl/ApiKey are still required by the options
            // validator even though the fake client never issues a request.
            ["WeakApp:BaseUrl"] = "http://weakapp.invalid",
            ["WeakApp:ApiKey"] = "test-api-key",
            ["WeakApp:PollingIntervalSeconds"] = pollingIntervalSeconds.ToString(CultureInfo.InvariantCulture),
        });

        // MassTransit connects in the background by default, so IHost.StartAsync would return before
        // the topology exists — and a StopAsync arriving in that window leaves an orphaned bus
        // consuming from the queues for the rest of the run.
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = StartStopTimeout;
            options.StopTimeout = StartStopTimeout;
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddSingleton(weakAppClient);
        builder.Services.AddSingleton(ingested);
        builder.Services.AddSingleton(attempts);

        builder.AddServiceMassTransit(
            bus =>
            {
                bus.AddConsumer<ReadingsIngestedCollectorConsumer>();
                bus.AddConsumer<IngestAttemptRecordedCollectorConsumer>();
            },
            (context, rabbitMq) =>
            {
                rabbitMq.AddReadingsReceiveEndpoint<ReadingsIngestedCollectorConsumer>(
                    context, "readings.ingested.e2e-capture", ReadingsTopology.IngestedRoutingKey);
                rabbitMq.AddReadingsReceiveEndpoint<IngestAttemptRecordedCollectorConsumer>(
                    context, "readings.attempt.e2e-capture", ReadingsTopology.AttemptRoutingKey);
            });

        // After the bus, exactly as in Program.cs: hosted services start in registration order and
        // the polling loop must not publish before the bus is connected.
        builder.AddIngestionPolling();

        var host = builder.Build();
        await host.StartAsync();

        return new IngestorEndToEndHost(host, ingested, attempts);
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
    }
}
