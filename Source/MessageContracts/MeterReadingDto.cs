namespace MessageContracts;

public sealed record MeterReadingDto(
    string Location,
    string MeterType,
    string MetricCode,
    decimal? Numeric,
    bool? Flag);
