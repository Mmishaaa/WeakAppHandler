namespace WeakAppHandler.Gateway.Application.Readings;

/// <summary>
/// One historical reading, with the owning meter's location and meter type denormalised onto it at
/// query time (via a SQL join) so the <c>readings</c> query can filter/sort by them without a
/// separate round trip or a GraphQL-level nested field.
/// </summary>
/// <remarks>
/// Init-only properties rather than a positional constructor, for the same reason as
/// <see cref="MeterReadModel"/>: HotChocolate's <c>[UseProjection]</c> needs a parameterless
/// constructor to build its per-field member-init expression.
/// </remarks>
public sealed record ReadingReadModel
{
    public required long Id { get; init; }

    public required Guid MeterId { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required string MetricCode { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    public decimal? ValueNumeric { get; init; }

    public bool? ValueBool { get; init; }

    public required bool IsChanged { get; init; }
}
