using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Application.Stats;
using WeakAppHandler.Processor.Infrastructure;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.ServiceDefaults.Messaging;

namespace WeakAppHandler.M2.IntegrationTests;

/// <summary>
/// The real Processor exactly as Program.cs wires it, including the real
/// <c>MeterReadingBatchWriter</c> (TASK-019's normalisation) — unlike
/// WeakAppHandler.Processor.Infrastructure.Tests' own ProcessorHost, which substitutes a fixed test
/// writer because normalisation is out of scope for the tests that host serves. An end-to-end test
/// needs the real writer: the eighteen-metric batch scenario asserts on the rows real normalisation
/// produces, not a stand-in's fixed output.
/// </summary>
internal sealed class ProcessorEndToEndHost(
    IHost host, ConsumeCounter consumed, MessageCollector<ReadingStored> storedReadings)
    : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    public ConsumeCounter Consumed => consumed;

    public MessageCollector<ReadingStored> StoredReadings => storedReadings;

    public IBus Bus => host.Services.GetRequiredService<IBus>();

    public ProcessingStatsState Stats => host.Services.GetRequiredService<ProcessingStatsState>();

    public static async Task<ProcessorEndToEndHost> StartAsync(IntegrationTestFixture fixture, string virtualHost)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var amqp = new Uri(fixture.RabbitMq.ConnectionString);
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Processor"] = fixture.Postgres.ConnectionString,
            ["RabbitMq:Host"] = amqp.Host,
            ["RabbitMq:Port"] = amqp.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:VirtualHost"] = virtualHost,
            ["RabbitMq:Username"] = RabbitMqIntegrationFixture.Username,
            ["RabbitMq:Password"] = RabbitMqIntegrationFixture.Password,
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // No IReadingBatchWriter override here, unlike WeakAppHandler.Processor.Infrastructure.Tests'
        // ProcessorHost: this suite wants the real normalisation AddProcessorInfrastructure registers.
        builder.Services.AddProcessorInfrastructure(builder.Configuration);

        var storedReadings = new MessageCollector<ReadingStored>();
        builder.Services.AddSingleton(storedReadings);

        // MassTransit connects in the background by default, so IHost.StartAsync would return before
        // the queues exist — and a StopAsync arriving in that window leaves an orphaned bus behind.
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = StartStopTimeout;
            options.StopTimeout = StartStopTimeout;
        });

        builder.AddServiceMassTransit(
            bus =>
            {
                bus.AddConsumer<ReadingsIngestedConsumer>();
                bus.AddConsumer<IngestAttemptRecordedConsumer>();
                bus.AddConsumer<ReadingStoredCollectorConsumer>();
            },
            (context, rabbitMq) =>
            {
                rabbitMq.AddReadingsReceiveEndpoint<ReadingsIngestedConsumer>(
                    context, ReadingsTopology.IngestedQueueName, ReadingsTopology.IngestedRoutingKey);
                rabbitMq.AddReadingsReceiveEndpoint<IngestAttemptRecordedConsumer>(
                    context, ReadingsTopology.AttemptQueueName, ReadingsTopology.AttemptRoutingKey);
                rabbitMq.AddReadingsReceiveEndpoint<ReadingStoredCollectorConsumer>(
                    context, ReadingsTopology.StoredQueueName, ReadingsTopology.StoredRoutingKey);
            });

        var host = builder.Build();
        await host.StartAsync();

        var consumed = new ConsumeCounter();
        host.Services.GetRequiredService<IBusControl>().ConnectConsumeObserver(consumed);

        return new ProcessorEndToEndHost(host, consumed, storedReadings);
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
    }
}
