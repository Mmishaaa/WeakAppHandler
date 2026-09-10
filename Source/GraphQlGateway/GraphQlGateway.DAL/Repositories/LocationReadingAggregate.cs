namespace GraphQlGateway.DAL.Repositories;

public sealed record LocationReadingAggregate(
    string Location,
    string MetricCode,
    int Count,
    decimal? Min,
    decimal? Max,
    decimal? Average);
