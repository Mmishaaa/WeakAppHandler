namespace GraphQlGateway.BLL.Models;

public sealed record LocationStatModel(
    string Location,
    string MetricCode,
    int Count,
    int TrueCount,
    decimal TrueShare,
    decimal? Min,
    decimal? Max,
    decimal? Average);
