using GraphQlGateway.BLL.Models;
using Shared.Entities;

namespace GraphQlGateway.BLL.Projections;

public static class ReadingListProjections
{
    extension(IEnumerable<DbReading> readings)
    {
        public IReadOnlyList<ReadingModel> ToModelList() => [.. readings.Select(Map)];
    }

    // Below the extension block because StyleCop does not recognise extension blocks yet and
    // reports any of them that follows a field or a property (SA1201).
    private static Func<DbReading, ReadingModel> Map { get; } = ReadingProjections.Projection.Compile();
}
