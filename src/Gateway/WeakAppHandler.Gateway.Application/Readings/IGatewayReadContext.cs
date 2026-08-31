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
}
