namespace DataProcessorService.BLL.Models;

public sealed record MeterReadingsBatchModel(
    Guid MessageId,
    Guid BatchId,
    DateTimeOffset CapturedAt,
    IReadOnlyList<MeterReadingModel> Readings);
