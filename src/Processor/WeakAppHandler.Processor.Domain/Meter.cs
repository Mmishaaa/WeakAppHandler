namespace WeakAppHandler.Processor.Domain;

public sealed class Meter
{
    public required Guid Id { get; init; }

    public required string Location { get; set; }

    public required string MeterType { get; set; }

    public required DateTimeOffset FirstSeenAt { get; set; }

    public required DateTimeOffset LastSeenAt { get; set; }
}
