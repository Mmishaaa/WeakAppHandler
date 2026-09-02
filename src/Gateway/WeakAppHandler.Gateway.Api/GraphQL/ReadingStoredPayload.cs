namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// What <c>onReadingStored</c> streams to a subscriber (PRD F4/F7), shaped like
/// <see cref="Application.Readings.ReadingReadModel"/> so a client already familiar with the
/// <c>readings</c> query recognises the fields. A type of its own rather than the Contracts message
/// itself: <see cref="WeakAppHandler.Contracts.ReadingStored"/> carries a wire <c>double</c>, and
/// this exposes the same <c>Decimal</c>/<c>Boolean</c> value pair the historical query already does.
/// </summary>
public sealed record ReadingStoredPayload
{
    public required Guid MeterId { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required string MetricCode { get; init; }

    public decimal? ValueNumeric { get; init; }

    public bool? ValueBool { get; init; }

    public required bool IsChanged { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }
}
