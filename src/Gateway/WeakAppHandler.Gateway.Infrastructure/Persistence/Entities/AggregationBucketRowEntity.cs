namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

/// <summary>
/// Shape of one row returned by <see cref="GatewayReadContext.GetAggregationsAsync"/>'s raw SQL. A
/// keyless entity type (no backing table): it exists only so EF Core has somewhere to map the result
/// of a <c>FromSqlInterpolated</c> query onto, never to be queried, inserted, updated or migrated.
/// </summary>
public sealed class AggregationBucketRowEntity
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
