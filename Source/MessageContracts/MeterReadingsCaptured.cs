namespace MessageContracts;

public sealed record MeterReadingsCaptured(
    Guid BatchId,
    DateTimeOffset CapturedAt,
    IReadOnlyList<MeterReadingDto> Readings);
