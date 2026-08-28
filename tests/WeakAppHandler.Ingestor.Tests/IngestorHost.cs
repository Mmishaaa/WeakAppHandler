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

namespace WeakAppHandler.Ingestor.Tests;

/// <summary>
/// The Ingestor host as Program.cs builds it — same <c>AddServiceMassTransit</c> with the shipped
/// topology, same <c>AddIngestionPolling</c>, same registration order — against a real broker, with
/// only the HTTP call to WeakApp faked. Everything downstream of the poller is production wiring.
/// </summary>
internal sealed class IngestorHost(
    IHost host,
    MessageCollector<ReadingsIngested> ingested,
    MessageCollector<IngestAttemptRecorded> attempts) : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    public MessageCollector<ReadingsIngested> Ingested => ingested;

    public MessageCollector<IngestAttemptRecorded> Attempts => attempts;

    public static async Task<IngestorHost> StartAsync(
        RabbitMqIntegrationFixture fixture,
        string virtualHost,
        IWeakAppClient weakAppClient)
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

            // The first poll runs immediately on startup, so the test never waits out an interval;
            // the value only has to be long enough that the loop does not poll again mid-assertion.
            // BaseUrl and ApiKey are still required by the options validator even though the fake
            // client never issues a request.
            ["WeakApp:BaseUrl"] = "http://weakapp.invalid",
            ["WeakApp:ApiKey"] = "test-api-key",
            ["WeakApp:PollingIntervalSeconds"] = "30",
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
                    context, ReadingsTopology.IngestedQueueName, ReadingsTopology.IngestedRoutingKey);
                rabbitMq.AddReadingsReceiveEndpoint<IngestAttemptRecordedCollectorConsumer>(
                    context, ReadingsTopology.AttemptQueueName, ReadingsTopology.AttemptRoutingKey);
            });

        // After the bus, exactly as in Program.cs: hosted services start in registration order and
        // the polling loop must not publish before the bus is connected.
        builder.AddIngestionPolling();

        var host = builder.Build();
        await host.StartAsync();

        return new IngestorHost(host, ingested, attempts);
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
    }
}
