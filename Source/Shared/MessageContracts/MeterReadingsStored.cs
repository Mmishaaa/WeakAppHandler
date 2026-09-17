namespace Shared.MessageContracts;

public sealed record MeterReadingsStored(
    Guid BatchId,
    DateTimeOffset StoredAt,
    IReadOnlyList<StoredMeterReadingDto> Readings);
