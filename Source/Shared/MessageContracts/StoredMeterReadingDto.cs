namespace Shared.MessageContracts;

public sealed record StoredMeterReadingDto(
    long Id,
    Guid MeterId,
    string Location,
    string MeterType,
    string MetricCode,
    DateTimeOffset ObservedAt,
    decimal? Numeric,
    bool? Flag);
