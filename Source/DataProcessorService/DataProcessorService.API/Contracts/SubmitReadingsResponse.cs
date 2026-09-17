namespace DataProcessorService.API.Contracts;

public sealed record SubmitReadingsResponse(
    Guid BatchId,
    DateTimeOffset CapturedAt,
    int ReadingCount);
