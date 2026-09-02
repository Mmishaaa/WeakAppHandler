using HotChocolate.Subscriptions;
using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// Bridges the <c>readings.stored</c> routing key onto HotChocolate's in-memory subscription topics
/// (PRD F4/F7 <c>onReadingStored</c>). A second, independent receive endpoint bound to the same
/// exchange/routing key Notification's <c>ReadingStoredConsumer</c> already consumes (TASK-029): the
/// topic exchange delivers a copy to every bound queue, so this does not compete with Notification
/// for deliveries, and Notification's own alert dispatch (which stays in-process, never on RabbitMQ)
/// is untouched by this consumer existing.
/// </summary>
public sealed class ReadingStoredSubscriptionConsumer(ITopicEventSender sender) : IConsumer<ReadingStored>
{
    /// <summary>The queue this consumer's receive endpoint binds, distinct from Notification's own <c>readings.stored</c> queue.</summary>
    public const string QueueName = "readings.stored.gateway";

    public async Task Consume(ConsumeContext<ReadingStored> context)
    {
        var message = context.Message;
        var payload = new ReadingStoredPayload
        {
            MeterId = message.MeterId,
            Location = message.Location,
            MeterType = message.MeterType,
            MetricCode = message.MetricCode,
            ValueNumeric = ToStorableDecimal(message.Value.Numeric),
            ValueBool = message.Value.Boolean,
            IsChanged = message.IsChanged,
            ObservedAt = message.ObservedAt,
        };

        var cancellationToken = context.CancellationToken;

        // Sent to every topic a subscriber's (location, meterType) arguments could resolve to -
        // unfiltered, location-only, meterType-only and both - rather than to one topic the resolver
        // then filters. Sending to a topic nobody has subscribed to is a no-op in HotChocolate's
        // in-memory provider, so fanning out here costs nothing when no subscriber asked for a given
        // combination.
        await sender.SendAsync(ReadingStoredTopics.Resolve(null, null), payload, cancellationToken);
        await sender.SendAsync(ReadingStoredTopics.Resolve(payload.Location, null), payload, cancellationToken);
        await sender.SendAsync(ReadingStoredTopics.Resolve(null, payload.MeterType), payload, cancellationToken);
        await sender.SendAsync(
            ReadingStoredTopics.Resolve(payload.Location, payload.MeterType), payload, cancellationToken);
    }

    // The wire value is a double (Contracts.MetricValue); GraphQL exposes Decimal here to match the
    // historical `readings` query. Range-checked before the cast, not after: casting a double outside
    // decimal's range throws OverflowException, and a poll result should never fault this consumer.
    private static decimal? ToStorableDecimal(double? value)
    {
        if (value is not double numeric || !double.IsFinite(numeric) || Math.Abs(numeric) > (double)decimal.MaxValue)
        {
            return null;
        }

        return (decimal)numeric;
    }
}
