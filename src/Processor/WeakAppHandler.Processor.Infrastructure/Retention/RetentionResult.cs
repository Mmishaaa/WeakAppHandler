namespace WeakAppHandler.Processor.Infrastructure.Retention;

/// <summary>What one retention run actually did, for the manual-trigger admin endpoint and logging.</summary>
public sealed record RetentionResult(
    DateTimeOffset CutoffUtc,
    int HourlyBucketsWritten,
    int IngestBatchesDeleted,
    int ProcessedMessagesDeleted);
