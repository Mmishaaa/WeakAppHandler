namespace GraphQlGateway.BLL.Models;

public sealed record MeterTypeStatModel(
    string MeterType,
    string MetricCode,
    int Count,
    int TrueCount,
    decimal TrueShare,
    decimal? Min,
    decimal? Max,
    decimal? Average);
