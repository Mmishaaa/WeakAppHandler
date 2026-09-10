namespace GraphQlGateway.BLL.Models;

public sealed record ReadingBucketModel(
    DateTimeOffset BucketStart,
    int Count,
    decimal? Min,
    decimal? Max,
    decimal? Average);
