namespace GraphQlGateway.BLL.Models;

public sealed record MeterModel
{
    public required Guid Id { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required DateTimeOffset FirstSeenAt { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }
}
