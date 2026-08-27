namespace WeakAppHandler.Processor.Domain;

public enum IngestBatchOutcome
{
    Success,
    HttpError,
    Timeout,
    Corrupted,
    RateLimited,
    Unauthorized,
}
