namespace DataIngestorService.Clients.Results;

enum PollOutcome
{
    Success,
    HttpError,
    Timeout,
    Corrupted,
    RateLimited,
    Unauthorized,
}
