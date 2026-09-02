namespace WeakAppHandler.Gateway.Application.Readings;

/// <summary>
/// The Gateway's only seam onto the core schema (PRD §4.3's read-only path). Query resolvers compose
/// on top of <see cref="Meters"/>/<see cref="Readings"/> (filtering, sorting, paging, projection);
/// the underlying <c>IQueryable</c> is EF Core's, so composing further translates into SQL rather
/// than materialising rows in memory.
/// </summary>
public interface IGatewayReadContext
{
    public IQueryable<MeterReadModel> Meters { get; }

    public IQueryable<ReadingReadModel> Readings { get; }

    /// <summary>
    /// Batches the current value of every metric for the given meters. A method rather than an
    /// <c>IQueryable</c> because it exists to be called from a DataLoader keyed by meter id, not to
    /// be filtered/sorted/paged by the caller.
    /// </summary>
    public Task<IReadOnlyList<MeterCurrentValueReadModel>> GetCurrentValuesAsync(
        IReadOnlyList<Guid> meterIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Buckets <paramref name="metricCode"/>'s readings into fixed-size time windows covering
    /// [<paramref name="from"/>, <paramref name="until"/>), one row per (location, meterType, bucket)
    /// combination - including buckets with no readings at all (PRD F4). A method rather than an
    /// <c>IQueryable</c>: bucket generation and the zero-fill join are expressed in a single SQL
    /// statement (<c>generate_series</c> + <c>date_trunc</c> + <c>GROUP BY</c>), not composed
    /// in-memory or built up by chaining further LINQ onto the result.
    /// </summary>
    /// <remarks>
    /// The upper bound isn't named <c>to</c> — a reserved keyword in some .NET languages (CA1716) —
    /// on a public interface member, matching the same <c>since</c>/<c>until</c> naming this
    /// codebase's <c>readings</c> query already uses for a time range in its GraphQL variables.
    /// </remarks>
    public Task<IReadOnlyList<AggregationBucketReadModel>> GetAggregationsAsync(
        string metricCode,
        AggregationBucketSize bucket,
        DateTimeOffset from,
        DateTimeOffset until,
        string? location,
        string? meterType,
        CancellationToken cancellationToken);
}
