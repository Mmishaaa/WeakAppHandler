namespace GraphQlGateway.BLL.Models;

public sealed record ReadingFilterModel(
    Guid? MeterId,
    string? Location,
    string? MeterType,
    string? MetricCode,
    DateTimeOffset? From,
    DateTimeOffset? To);
