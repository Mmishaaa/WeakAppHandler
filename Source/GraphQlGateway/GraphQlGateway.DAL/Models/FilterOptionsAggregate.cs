namespace GraphQlGateway.DAL.Models;

public sealed record FilterOptionsAggregate(
    IReadOnlyList<string> Locations,
    IReadOnlyList<string> MeterTypes,
    IReadOnlyList<string> MetricCodes);
