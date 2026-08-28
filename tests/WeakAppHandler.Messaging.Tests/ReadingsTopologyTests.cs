using System.Text;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using WeakAppHandler.Contracts;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.ServiceDefaults.Messaging;

namespace WeakAppHandler.Messaging.Tests;

/// <summary>
/// TASK-012's acceptance criteria, run against a real broker: the topology is declared rather than
/// assumed, a persistent message outlives a broker restart, and a message whose consumer never
/// succeeds ends up in the dead-letter queue.
/// </summary>
[Collection(MessagingCollectionDefinition.Name)]
public sealed class ReadingsTopologyTests : IAsyncLifetime, IDisposable
{
    private const int RetryCount = 2;

    private const int RetryIntervalMilliseconds = 100;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ReadPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly RabbitMqIntegrationFixture _fixture;
    private readonly RabbitMqManagementClient _management;

    // The entity names are fixed by the PRD, so a per-test vhost is what keeps one test's messages
    // out of another test's queue-depth assertions while still exercising the real names.
    private readonly string _virtualHost = $"task012-{Guid.NewGuid():N}";

    public ReadingsTopologyTests(RabbitMqIntegrationFixture fixture)
    {
        _fixture = fixture;
        _management = new RabbitMqManagementClient(fixture);
    }

    public Task InitializeAsync() => _fixture.CreateVirtualHostAsync(_virtualHost);

    public Task DisposeAsync() => _fixture.DeleteVirtualHostAsync(_virtualHost);

    // xUnit runs IAsyncLifetime.DisposeAsync before IDisposable.Dispose, so the vhost is torn down
    // over the client above before the client itself goes away here.
    public void Dispose() => _management.Dispose();

    [Fact]
    public async Task Bus_OnStartup_DeclaresTheReadingsExchangeAndItsDurableQueues()
    {
        await using var host = await StartConsumingHostAsync();

        var exchange = await _management.FindExchangeAsync(_virtualHost, ReadingsTopology.ExchangeName);

        Assert.NotNull(exchange);
        Assert.Equal("topic", exchange.Type);
        Assert.True(exchange.Durable, "The ingestion exchange must be durable to survive a broker restart.");
        Assert.False(exchange.AutoDelete);

        // Only the live queues are expected here. MassTransit declares an endpoint's "_error" queue
        // the first time it has to move a message into it, not at startup, so asserting it exists
        // now would be asserting something the transport does not promise; that the dead-lettering
        // works is covered by Message_WhoseConsumerAlwaysThrows_EndsUpInTheDeadLetterQueue.
        string[] queueNames = [ReadingsTopology.IngestedQueueName, ReadingsTopology.AttemptQueueName];

        foreach (var queueName in queueNames)
        {
            var queue = await _management.FindQueueAsync(_virtualHost, queueName);

            Assert.True(queue is not null, $"Queue '{queueName}' was not declared on the broker.");
            Assert.True(queue!.Durable, $"Queue '{queueName}' must be durable.");
            Assert.False(queue.AutoDelete, $"Queue '{queueName}' must not be auto-delete.");
        }
    }

    [Fact]
    public async Task Bus_OnStartup_BindsEachQueueToItsOwnRoutingKeyOnly()
    {
        await using var host = await StartConsumingHostAsync();

        var bindings = await _management.GetBindingsFromExchangeAsync(_virtualHost, ReadingsTopology.ExchangeName);

        Assert.Contains(bindings, b =>
            b.Destination == ReadingsTopology.IngestedQueueName
            && b.RoutingKey == ReadingsTopology.IngestedRoutingKey);

        Assert.Contains(bindings, b =>
            b.Destination == ReadingsTopology.AttemptQueueName
            && b.RoutingKey == ReadingsTopology.AttemptRoutingKey);

        // The point of a topic exchange over the per-message-type fanout exchanges MassTransit would
        // otherwise create: a queue must not receive the other message type's traffic.
        Assert.DoesNotContain(bindings, b =>
            b.Destination == ReadingsTopology.IngestedQueueName
            && b.RoutingKey == ReadingsTopology.AttemptRoutingKey);

        Assert.DoesNotContain(bindings, b =>
            b.Destination == ReadingsTopology.AttemptQueueName
            && b.RoutingKey == ReadingsTopology.IngestedRoutingKey);
    }

    [Fact]
    public async Task PublishedMessage_IsPersistent_AndSurvivesABrokerRestart()
    {
        // Declaring the topology and publishing into it are two separate bus instances on purpose:
        // the consuming host's endpoints would drain the message long before the broker restarts.
        await (await StartConsumingHostAsync()).DisposeAsync();

        var batchId = Guid.NewGuid();

        await using (var publisher = await MessagingHost.StartAsync(_fixture, _virtualHost))
        {
            await publisher.Bus.Publish(new ReadingsIngested(
                MessageId: Guid.NewGuid(),
                BatchId: batchId,
                FetchedAt: DateTimeOffset.UtcNow,
                SourceLatencyMs: 42,
                Readings: [new MeterReadingEnvelope("boiler-1", "temperature", "{}", "hash-1")]));
        }

        await _management.WaitForQueueAsync(
            _virtualHost, ReadingsTopology.IngestedQueueName, q => q.Messages == 1, Timeout);

        await _fixture.RestartBrokerAsync();
        await _management.WaitUntilReadyAsync(Timeout);

        var afterRestart = await _management.WaitForQueueAsync(
            _virtualHost, ReadingsTopology.IngestedQueueName, q => q.Messages == 1, Timeout);

        Assert.Equal(1, afterRestart.Messages);

        var message = await ReadOneAsync(ReadingsTopology.IngestedQueueName);

        Assert.True(message.Persistent, "The message must be published with the persistent delivery mode.");
        Assert.Contains(batchId.ToString(), message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Message_WhoseConsumerAlwaysThrows_EndsUpInTheDeadLetterQueue()
    {
        var attempts = new ConsumeAttemptCounter();
        var batchId = Guid.NewGuid();

        await using (var host = await MessagingHost.StartAsync(
            _fixture,
            _virtualHost,
            bus => bus.AddConsumer<AlwaysFailingAttemptConsumer>(),
            (context, rabbitMq) => rabbitMq.AddReadingsReceiveEndpoint<AlwaysFailingAttemptConsumer>(
                context,
                ReadingsTopology.AttemptQueueName,
                ReadingsTopology.AttemptRoutingKey,
                RetryCount,
                RetryIntervalMilliseconds),
            services => services.AddSingleton(attempts)))
        {
            await host.Bus.Publish(new IngestAttemptRecorded(
                MessageId: Guid.NewGuid(),
                BatchId: batchId,
                FetchedAt: DateTimeOffset.UtcNow,
                Outcome: IngestOutcome.Success,
                HttpStatus: 200,
                DurationMs: 17,
                ReadingCount: 1,
                ErrorMessage: null));

            await attempts.WaitForAsync(RetryCount + 1, Timeout);
        }

        // This is also where the dead-letter queue itself first appears: MassTransit declares it when
        // it has a message to move into it rather than when the endpoint starts.
        var deadLetterQueue = await _management.WaitForQueueAsync(
            _virtualHost, ReadingsTopology.AttemptDeadLetterQueueName, q => q.Messages == 1, Timeout);

        Assert.Equal(1, deadLetterQueue.Messages);
        Assert.True(deadLetterQueue.Durable, "The dead-letter queue must be durable.");

        // The retry policy has to be what gave up, not the transport: one delivery plus RetryCount
        // redeliveries, and then the message moves rather than being redelivered forever.
        Assert.Equal(RetryCount + 1, attempts.Count);

        var deadLettered = await ReadOneAsync(ReadingsTopology.AttemptDeadLetterQueueName);

        Assert.Contains(batchId.ToString(), deadLettered.Body, StringComparison.OrdinalIgnoreCase);

        var liveQueue = await _management.FindQueueAsync(_virtualHost, ReadingsTopology.AttemptQueueName);

        Assert.NotNull(liveQueue);
        Assert.Equal(0, liveQueue.Messages);
    }

    private Task<MessagingHost> StartConsumingHostAsync() => MessagingHost.StartAsync(
        _fixture,
        _virtualHost,
        bus =>
        {
            bus.AddConsumer<NoOpReadingsIngestedConsumer>();
            bus.AddConsumer<AlwaysFailingAttemptConsumer>();
        },
        (context, rabbitMq) =>
        {
            rabbitMq.AddReadingsReceiveEndpoint<NoOpReadingsIngestedConsumer>(
                context, ReadingsTopology.IngestedQueueName, ReadingsTopology.IngestedRoutingKey);

            rabbitMq.AddReadingsReceiveEndpoint<AlwaysFailingAttemptConsumer>(
                context, ReadingsTopology.AttemptQueueName, ReadingsTopology.AttemptRoutingKey);
        },
        services => services.AddSingleton<ConsumeAttemptCounter>());

    /// <summary>
    /// Reads one message straight off a queue with the raw AMQP client, retrying while the queue
    /// reads as empty. MassTransit deserialises the frame properties away, and the delivery mode the
    /// broker actually stored is the whole point of the persistence assertion. Polling matters
    /// because the management API's queue depths come from a statistics database that can still be
    /// serving pre-restart numbers when the broker has only just come back up — so an empty first
    /// read is not on its own evidence that the message is gone.
    /// </summary>
    private async Task<DeliveredMessage> ReadOneAsync(string queueName)
    {
        var amqp = new Uri(_fixture.ConnectionString);

        var factory = new ConnectionFactory
        {
            HostName = amqp.Host,
            Port = amqp.Port,
            UserName = RabbitMqIntegrationFixture.Username,
            Password = RabbitMqIntegrationFixture.Password,
            VirtualHost = _virtualHost,
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var deadline = DateTime.UtcNow + Timeout;

        while (true)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: true);

            if (result is not null)
            {
                return new DeliveredMessage(
                    result.BasicProperties.Persistent,
                    Encoding.UTF8.GetString(result.Body.Span));
            }

            Assert.True(DateTime.UtcNow < deadline, $"Queue '{queueName}' was still empty after {Timeout}.");

            await Task.Delay(ReadPollInterval);
        }
    }
}
