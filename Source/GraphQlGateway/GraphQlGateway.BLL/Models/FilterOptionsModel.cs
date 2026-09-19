namespace GraphQlGateway.BLL.Models;

public sealed record FilterOptionsModel(
    IReadOnlyList<string> Locations,
    IReadOnlyList<string> MeterTypes,
    IReadOnlyList<string> MetricCodes);
