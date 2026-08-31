using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.ServiceDefaults.Messaging;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// The Processor as Program.cs builds it — the same <c>AddProcessorInfrastructure</c>, the same
/// consumers on the same receive endpoints of the shipped topology — against a real broker and a
/// real database. Only the reading writer is substituted, for the reason its interface exists
/// (normalisation is TASK-019); everything the message path itself does is production wiring.
/// </summary>
internal sealed class ProcessorHost(IHost host, ConsumeCounter consumed) : IAsyncDisposable
{
    private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(60);

    public ConsumeCounter Consumed => consumed;

    public IBus Bus => host.Services.GetRequiredService<IBus>();

    public static async Task<ProcessorHost> StartAsync(IntegrationTestFixture fixture, string virtualHost)
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

        // Registered before the infrastructure, whose registration is a TryAdd: this is the seam
        // TASK-019 fills in, and a writer that stores nothing would make "the readings were stored
        // once" unobservable.
        builder.Services.AddScoped<IReadingBatchWriter, TestReadingBatchWriter>();
        builder.Services.AddProcessorInfrastructure(builder.Configuration);

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
            },
            (context, rabbitMq) =>
            {
                rabbitMq.AddReadingsReceiveEndpoint<ReadingsIngestedConsumer>(
                    context, ReadingsTopology.IngestedQueueName, ReadingsTopology.IngestedRoutingKey);
                rabbitMq.AddReadingsReceiveEndpoint<IngestAttemptRecordedConsumer>(
                    context, ReadingsTopology.AttemptQueueName, ReadingsTopology.AttemptRoutingKey);
            });

        var host = builder.Build();
        await host.StartAsync();

        var consumed = new ConsumeCounter();
        host.Services.GetRequiredService<IBusControl>().ConnectConsumeObserver(consumed);

        return new ProcessorHost(host, consumed);
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
    }
}
