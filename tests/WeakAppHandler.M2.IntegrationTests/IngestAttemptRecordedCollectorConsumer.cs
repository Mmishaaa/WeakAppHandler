using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.M2.IntegrationTests;

internal sealed class IngestAttemptRecordedCollectorConsumer(MessageCollector<IngestAttemptRecorded> collector)
    : IConsumer<IngestAttemptRecorded>
{
    public Task Consume(ConsumeContext<IngestAttemptRecorded> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        collector.Add(context.Message);
        return Task.CompletedTask;
    }
}
