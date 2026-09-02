namespace WeakAppHandler.Gateway.Application.Readings;

/// <summary>
/// One time bucket of a single metric's readings for one (location, meterType) pair. Every bucket in
/// the requested range is present exactly once, even when no reading fell into it - <c>Count</c> is
/// <c>0</c> and <c>Avg</c>/<c>Min</c>/<c>Max</c>/<c>Sum</c> are <c>null</c> in that case, matching how
/// <see cref="ReadingReadModel.ValueNumeric"/> already represents "no value" (PRD F4's requirement
/// that empty buckets be represented explicitly, not omitted).
/// </summary>
public sealed record AggregationBucketReadModel
{
    public required DateTimeOffset BucketStart { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public decimal? Avg { get; init; }

    public decimal? Min { get; init; }

    public decimal? Max { get; init; }

    public decimal? Sum { get; init; }

    public required int Count { get; init; }
}
