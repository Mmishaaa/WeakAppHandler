using MassTransit;
using WeakAppHandler.Contracts;

namespace WeakAppHandler.Messaging.Tests;

// The "consumer that keeps falling over" from TASK-012's second test step: it throws on every
// delivery, including every retry, so the message can only leave the queue by being dead-lettered.
internal sealed class AlwaysFailingAttemptConsumer(ConsumeAttemptCounter attempts) : IConsumer<IngestAttemptRecorded>
{
    public Task Consume(ConsumeContext<IngestAttemptRecorded> context)
    {
        attempts.Record();

        throw new InvalidOperationException("This consumer fails on every delivery, by design.");
    }
}
