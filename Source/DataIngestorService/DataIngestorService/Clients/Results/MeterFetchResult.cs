using DataIngestorService.Contracts;

namespace DataIngestorService.Clients.Results;

sealed record MeterFetchResult
{
    public required PollOutcome Outcome { get; init; }

    public int? HttpStatusCode { get; init; }

    public IReadOnlyList<WeakAppMeterDto> Meters { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public required TimeSpan Duration { get; init; }
}
