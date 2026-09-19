namespace GraphQlGateway.DAL.Repositories;

public sealed record MeterTypeReadingAggregate(
    string MeterType,
    string MetricCode,
    int Count,
    int TrueCount,
    decimal TrueShare,
    decimal? Min,
    decimal? Max,
    decimal? Average);
