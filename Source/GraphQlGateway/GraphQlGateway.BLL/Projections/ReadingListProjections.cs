using GraphQlGateway.BLL.Models;
using Shared.Entities;

namespace GraphQlGateway.BLL.Projections;

public static class ReadingListProjections
{
    private static Func<DbReading, ReadingModel> Map { get; } = ReadingProjections.Projection.Compile();

    extension(IEnumerable<DbReading> readings)
    {
        public IReadOnlyList<ReadingModel> ToModelList() => [.. readings.Select(Map)];
    }
}
