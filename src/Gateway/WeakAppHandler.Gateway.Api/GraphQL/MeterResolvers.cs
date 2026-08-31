using HotChocolate;
using HotChocolate.Types;
using WeakAppHandler.Gateway.Application.Readings;

namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>Adds the <c>currentValues</c> field onto <see cref="MeterReadModel"/> (PRD F4's "meters ... with their current values").</summary>
[ExtendObjectType(typeof(MeterReadModel))]
public sealed class MeterResolvers
{
    public async Task<IReadOnlyList<MeterCurrentValueReadModel>> GetCurrentValuesAsync(
        [Parent] MeterReadModel meter,
        MeterCurrentValuesDataLoader dataLoader,
        CancellationToken cancellationToken)
    {
        var values = await dataLoader.LoadAsync(meter.Id, cancellationToken);
        return values ?? [];
    }
}
