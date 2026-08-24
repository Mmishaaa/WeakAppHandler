namespace WeakAppHandler.Contracts;

/// <summary>
/// The result of a single Ingestor poll attempt against WeakApp, mirroring the
/// <c>ingest_batches.outcome</c> domain.
/// </summary>
public enum IngestOutcome
{
    Success,
    HttpError,
    Timeout,
    Corrupted,
    RateLimited,
    Unauthorized,
}
