namespace DataProcessorService.API.Contracts;

public sealed record SubmitReadingRequest(
    string? Location,
    string? MeterType,
    string? MetricCode,
    decimal? Numeric,
    bool? Flag);
