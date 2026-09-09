namespace DataProcessorService.BLL.Models;

public sealed record MeterReadingModel(
    string Location,
    string MeterType,
    string MetricCode,
    decimal? Numeric,
    bool? Flag);
