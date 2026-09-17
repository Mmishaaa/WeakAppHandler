namespace DataProcessorService.BLL.Models;

public sealed record StoredReadingModel(
    long Id,
    Guid MeterId,
    string Location,
    string MeterType,
    string MetricCode,
    DateTimeOffset ObservedAt,
    decimal? Numeric,
    bool? Flag);
