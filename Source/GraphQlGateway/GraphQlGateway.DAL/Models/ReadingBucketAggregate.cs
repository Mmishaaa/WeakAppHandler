namespace GraphQlGateway.DAL.Models;

public sealed record ReadingBucketAggregate(
    string Location,
    int Year,
    int Month,
    int Day,
    int Hour,
    int Minute,
    int Count,
    int TrueCount,
    decimal TrueShare,
    decimal? Min,
    decimal? Max,
    decimal? Average);
