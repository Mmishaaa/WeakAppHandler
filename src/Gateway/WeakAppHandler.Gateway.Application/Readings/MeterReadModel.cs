namespace WeakAppHandler.Gateway.Application.Readings;

/// <summary>
/// A meter as the read model exposes it: no domain behaviour, just the columns GraphQL needs to
/// project. Deliberately its own type rather than a reference to Processor.Domain.Meter, so the
/// Gateway never takes a compile-time dependency on the service that owns and migrates this schema.
/// </summary>
/// <remarks>
/// Init-only properties rather than a positional constructor: HotChocolate's <c>[UseProjection]</c>
/// rewrites the SQL projection per GraphQL field selection by building a member-init expression
/// (<c>new MeterReadModel { Id = ..., ... }</c>), which requires a parameterless constructor - a
/// positional record's synthesized constructor is not one.
/// </remarks>
public sealed record MeterReadModel
{
    public required Guid Id { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required DateTimeOffset FirstSeenAt { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }
}
