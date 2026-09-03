using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.M2.IntegrationTests;

internal sealed class ReadingStoredCollectorConsumer(MessageCollector<ReadingStored> collector)
    : IConsumer<ReadingStored>
{
    public Task Consume(ConsumeContext<ReadingStored> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        collector.Add(context.Message);
        return Task.CompletedTask;
    }
}
