using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// The Gateway's GraphQL root subscription type (PRD F4/F7): pushes <c>ReadingStored</c> events
/// straight from RabbitMQ, not from a database poll - the whole reason
/// <see cref="ReadingStoredSubscriptionConsumer"/> exists as a receive endpoint of its own rather
/// than this resolving off <see cref="Application.Readings.IGatewayReadContext"/>.
/// </summary>
public sealed class Subscription
{
    /// <summary>
    /// <paramref name="location"/>/<paramref name="meterType"/> narrow the stream to a single
    /// location and/or meter type; omitted, the subscriber receives every stored reading.
    /// </summary>
    [Subscribe(With = nameof(SubscribeToOnReadingStoredAsync))]
    public ReadingStoredPayload OnReadingStored(
        [EventMessage] ReadingStoredPayload payload,
        string? location,
        string? meterType) => payload;

    public ValueTask<ISourceStream<ReadingStoredPayload>> SubscribeToOnReadingStoredAsync(
        string? location,
        string? meterType,
        [Service] ITopicEventReceiver receiver,
        CancellationToken cancellationToken) =>
        receiver.SubscribeAsync<ReadingStoredPayload>(
            ReadingStoredTopics.Resolve(location, meterType), cancellationToken);
}
