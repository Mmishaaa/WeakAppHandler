namespace GraphQlGateway.BLL.Models;

public sealed record ReadingStatsModel(
    string MetricCode,
    TimeBucket Bucket,
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? MeterId,
    string? Location);
