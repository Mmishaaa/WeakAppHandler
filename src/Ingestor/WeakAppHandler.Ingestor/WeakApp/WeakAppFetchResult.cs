using WeakAppHandler.Contracts;

namespace WeakAppHandler.Ingestor.WeakApp;

/// <summary>
/// The outcome of one <see cref="IWeakAppClient.GetMetersAsync"/> call, after the resilience
/// pipeline (retry/circuit-breaker/timeout) has already run. TASK-016's polling loop maps this
/// into <see cref="IngestAttemptRecorded"/>/<see cref="ReadingsIngested"/> with a shared batch id.
/// </summary>
public sealed record WeakAppFetchResult
{
    public required IngestOutcome Outcome { get; init; }

    public int? HttpStatusCode { get; init; }

    public IReadOnlyList<WeakAppMeterDto> Meters { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public required TimeSpan Duration { get; init; }
}
