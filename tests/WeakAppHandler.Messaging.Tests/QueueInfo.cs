namespace WeakAppHandler.Messaging.Tests;

// One row of the management UI's Queues page.
internal sealed record QueueInfo(string Name, bool Durable, bool AutoDelete, int Messages);
