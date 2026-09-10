namespace GraphQlGateway.DAL.Repositories;

public sealed record HourlyReadingAggregate(
    int Year,
    int Month,
    int Day,
    int Hour,
    int Count,
    decimal? Min,
    decimal? Max,
    decimal? Sum);
