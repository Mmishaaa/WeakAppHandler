namespace DataProcessorService.API.Contracts;

public sealed record SubmitReadingsRequest(
    DateTimeOffset? CapturedAt,
    IReadOnlyList<SubmitReadingRequest>? Readings);
