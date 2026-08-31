namespace WeakAppHandler.Processor.Application.Ingestion;

/// <summary>
/// What consuming one ingestion message did. RabbitMQ delivers at least once, so a redelivery is a
/// normal, expected outcome rather than a failure — the consumer acknowledges it either way, and
/// TASK-021's <c>/stats</c> endpoint counts the two apart.
/// </summary>
public enum IngestionRecordResult
{
    /// <summary>The message was new; its effects are committed.</summary>
    Recorded,

    /// <summary>The message id was already in <c>processed_messages</c>; nothing was written.</summary>
    Duplicate,
}
