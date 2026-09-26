namespace DataIngestorService.Clients.Results;

internal enum PollOutcome
{
    Success,
    HttpError,
    Timeout,
    Corrupted,
    RateLimited,
    Unauthorized,
}
