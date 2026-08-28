namespace WeakAppHandler.Messaging.Tests;

// A single message read straight off a queue over AMQP, carrying the two things the topology tests
// need to check: whether the broker stored it as persistent, and which batch it belongs to.
internal sealed record DeliveredMessage(bool Persistent, string Body);
