namespace DataProcessorService.BLL.Models;

public sealed record MeterReadingsBatchModel(
    Guid BatchId,
    DateTimeOffset CapturedAt,
    IReadOnlyList<MeterReadingModel> Readings);
