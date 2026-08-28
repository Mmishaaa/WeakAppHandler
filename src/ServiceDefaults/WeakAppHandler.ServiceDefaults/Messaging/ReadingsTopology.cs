namespace WeakAppHandler.ServiceDefaults.Messaging;

/// <summary>
/// Names of the RabbitMQ entities that carry the ingestion pipeline (PRD F2). Shared by the
/// publishing side (Ingestor) and the consuming side (Processor) so a rename breaks the build
/// rather than silently unbinding a queue at runtime.
/// </summary>
public static class ReadingsTopology
{
    /// <summary>
    /// The single topic exchange every ingestion message is published to. Both message types
    /// share it and are discriminated by routing key, so a subscriber can bind to one kind of
    /// message without receiving the other.
    /// </summary>
    public const string ExchangeName = "readings.exchange";

    public const string IngestedRoutingKey = "readings.ingested";

    public const string AttemptRoutingKey = "readings.attempt";

    public const string IngestedQueueName = "readings.ingested";

    public const string AttemptQueueName = "readings.attempt";

    /// <summary>
    /// MassTransit moves a message that exhausts its retry policy to a queue named after the
    /// receive endpoint with this suffix. The DLQ names below are derived from it rather than
    /// written out, so they cannot drift from the queues they belong to. Note the queue is declared
    /// the first time a message actually has to be moved into it, not when the endpoint starts, so
    /// an empty dead-letter queue is absent from the management UI rather than shown at zero.
    /// </summary>
    public const string DeadLetterQueueSuffix = "_error";

    public const string IngestedDeadLetterQueueName = IngestedQueueName + DeadLetterQueueSuffix;

    public const string AttemptDeadLetterQueueName = AttemptQueueName + DeadLetterQueueSuffix;
}
