using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.M2.IntegrationTests;

internal sealed class ReadingsIngestedCollectorConsumer(MessageCollector<ReadingsIngested> collector)
    : IConsumer<ReadingsIngested>
{
    public Task Consume(ConsumeContext<ReadingsIngested> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        collector.Add(context.Message);
        return Task.CompletedTask;
    }
}
