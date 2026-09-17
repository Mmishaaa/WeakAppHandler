namespace DataProcessorService.BLL.Models;

public sealed record ReadingQueryModel(
    Guid MeterId,
    string? MetricCode,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);
