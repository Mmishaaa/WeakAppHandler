namespace GraphQlGateway.DAL.Repositories;

public sealed record ReadingBucketAggregate(
    string Location,
    int Year,
    int Month,
    int Day,
    int Hour,
    int Count,
    decimal? Min,
    decimal? Max,
    decimal? Average);
