namespace WeakAppHandler.Processor.Domain;

public sealed class ProcessedMessage
{
    public required Guid MessageId { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}
