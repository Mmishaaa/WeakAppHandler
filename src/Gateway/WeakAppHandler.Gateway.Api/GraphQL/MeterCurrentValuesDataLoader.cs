using GreenDonut;
using WeakAppHandler.Gateway.Application.Readings;

namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// Batches <c>meter_current_state</c> lookups for the <c>currentValues</c> field across every meter
/// requested in one GraphQL selection, instead of one query per meter (PRD F4's "no over-fetching"
/// behaviour applied to a nested field that projection alone cannot cover).
/// </summary>
public sealed class MeterCurrentValuesDataLoader(
    IGatewayReadContext context,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : BatchDataLoader<Guid, IReadOnlyList<MeterCurrentValueReadModel>>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MeterCurrentValueReadModel>>> LoadBatchAsync(
        IReadOnlyList<Guid> keys,
        CancellationToken cancellationToken)
    {
        var values = await context.GetCurrentValuesAsync(keys, cancellationToken);

        return values
            .GroupBy(v => v.MeterId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MeterCurrentValueReadModel>)g.ToList());
    }
}
