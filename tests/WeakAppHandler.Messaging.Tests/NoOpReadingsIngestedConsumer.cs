using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Messaging.Tests;

// Exists only to give the readings.ingested receive endpoint a consumer, since MassTransit declares
// an endpoint's queue and bindings as a side effect of hosting one. It must not be running while a
// test inspects queue depth, which is why the topology and publishing hosts are kept separate.
internal sealed class NoOpReadingsIngestedConsumer : IConsumer<ReadingsIngested>
{
    public Task Consume(ConsumeContext<ReadingsIngested> context) => Task.CompletedTask;
}
