namespace GraphQlGateway.DAL.Models;

public sealed record LocationReadingAggregate(
    string Location,
    string MetricCode,
    int Count,
    int TrueCount,
    decimal TrueShare,
    decimal? Min,
    decimal? Max,
    decimal? Average);
