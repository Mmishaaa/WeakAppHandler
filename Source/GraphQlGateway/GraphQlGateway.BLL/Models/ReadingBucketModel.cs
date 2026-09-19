namespace GraphQlGateway.BLL.Models;

public sealed record ReadingBucketModel(
    DateTimeOffset BucketStart,
    int Count,
    int TrueCount,
    decimal TrueShare,
    decimal? Min,
    decimal? Max,
    decimal? Average);
