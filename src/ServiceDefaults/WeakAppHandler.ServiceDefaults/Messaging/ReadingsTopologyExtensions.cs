using MassTransit;
using RabbitMQ.Client;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.ServiceDefaults.Messaging;

/// <summary>
/// Declarative RabbitMQ topology for the ingestion pipeline (PRD F2). MassTransit is the single
/// source of truth here rather than deploy/rabbitmq/definitions.json: declaring the same entity
/// in both places only works while every property matches exactly, and a drift shows up as a
/// PRECONDITION_FAILED that takes the whole bus down at startup. definitions.json therefore only
/// carries broker-level concerns (vhost, users, permissions) that MassTransit never declares.
/// </summary>
public static class ReadingsTopologyExtensions
{
    private const int DefaultRetryCount = 3;

    private const int DefaultRetryIntervalMilliseconds = 1000;

    /// <summary>
    /// Points both ingestion message types at <see cref="ReadingsTopology.ExchangeName"/> as a
    /// durable topic exchange and pins each one's routing key. Without this, MassTransit's default
    /// topology would publish each message type to its own fanout exchange named after the CLR
    /// type, which is neither the exchange nor the routing scheme the PRD specifies.
    /// </summary>
    public static void ConfigureReadingsTopology(this IRabbitMqBusFactoryConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        ConfigurePublishedMessage<ReadingsIngested>(configurator, ReadingsTopology.IngestedRoutingKey);
        ConfigurePublishedMessage<IngestAttemptRecorded>(configurator, ReadingsTopology.AttemptRoutingKey);
    }

    /// <summary>
    /// Declares a durable receive endpoint bound to <see cref="ReadingsTopology.ExchangeName"/>
    /// by routing key, with a retry policy after which MassTransit moves the message to the
    /// endpoint's dead-letter queue (<paramref name="queueName"/> +
    /// <see cref="ReadingsTopology.DeadLetterQueueSuffix"/>).
    /// </summary>
    public static void AddReadingsReceiveEndpoint<TConsumer>(
        this IRabbitMqBusFactoryConfigurator configurator,
        IRegistrationContext context,
        string queueName,
        string routingKey,
        int retryCount = DefaultRetryCount,
        int retryIntervalMilliseconds = DefaultRetryIntervalMilliseconds)
        where TConsumer : class, IConsumer
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(context);

        configurator.ReceiveEndpoint(queueName, endpoint =>
        {
            endpoint.Durable = true;
            endpoint.AutoDelete = false;

            // The default consume topology would bind this endpoint to a per-message-type fanout
            // exchange, which would deliver every ingestion message regardless of routing key and
            // defeat the discrimination the topic exchange exists to provide.
            endpoint.ConfigureConsumeTopology = false;
            endpoint.Bind(ReadingsTopology.ExchangeName, binding =>
            {
                binding.RoutingKey = routingKey;
                binding.ExchangeType = ExchangeType.Topic;
                binding.Durable = true;
                binding.AutoDelete = false;
            });

            endpoint.UseMessageRetry(retry =>
                retry.Interval(retryCount, TimeSpan.FromMilliseconds(retryIntervalMilliseconds)));

            endpoint.ConfigureConsumer<TConsumer>(context);
        });
    }

    private static void ConfigurePublishedMessage<TMessage>(
        IRabbitMqBusFactoryConfigurator configurator,
        string routingKey)
        where TMessage : class
    {
        configurator.Message<TMessage>(message => message.SetEntityName(ReadingsTopology.ExchangeName));

        configurator.Publish<TMessage>(publish =>
        {
            publish.ExchangeType = ExchangeType.Topic;
            publish.Durable = true;
            publish.AutoDelete = false;
        });

        configurator.Send<TMessage>(send => send.UseRoutingKeyFormatter(_ => routingKey));
    }
}
