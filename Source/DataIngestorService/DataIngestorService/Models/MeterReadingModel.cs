namespace DataIngestorService.Models;

internal sealed record MeterReadingModel(
    string Location,
    string MeterType,
    string MetricCode,
    decimal? Numeric,
    bool? Flag);
