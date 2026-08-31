namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Entities;

/// <summary>
/// Maps onto the <c>meters</c> table Processor owns and migrates (PRD §7.1). The Gateway never
/// creates or alters this table - it only ever selects from it - so this type exists purely to give
/// EF Core something to map columns onto; it carries no domain behaviour.
/// </summary>
public sealed class MeterEntity
{
    public required Guid Id { get; init; }

    public required string Location { get; init; }

    public required string MeterType { get; init; }

    public required DateTimeOffset FirstSeenAt { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }
}
