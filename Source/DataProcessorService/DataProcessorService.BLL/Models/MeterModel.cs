namespace DataProcessorService.BLL.Models;

public sealed record MeterModel(
    Guid Id,
    string Location,
    string MeterType,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
