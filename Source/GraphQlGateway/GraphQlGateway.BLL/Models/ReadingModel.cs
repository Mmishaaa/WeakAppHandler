namespace GraphQlGateway.BLL.Models;

public sealed record ReadingModel
{
    public required long Id { get; init; }

    public required Guid MeterId { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required string MetricCode { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    public decimal? Numeric { get; init; }

    public bool? Flag { get; init; }
}
