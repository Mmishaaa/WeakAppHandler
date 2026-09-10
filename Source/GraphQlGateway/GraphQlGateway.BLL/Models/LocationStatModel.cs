namespace GraphQlGateway.BLL.Models;

public sealed record LocationStatModel(
    string Location,
    string MetricCode,
    int Count,
    decimal? Min,
    decimal? Max,
    decimal? Average);
