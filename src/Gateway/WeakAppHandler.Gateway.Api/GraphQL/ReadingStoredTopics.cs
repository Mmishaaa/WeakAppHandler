namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// Topic keys for HotChocolate's in-memory pub/sub behind <c>onReadingStored</c> (PRD F4).
/// <c>location</c>/<c>meterType</c> are optional filter arguments (null means "any"), so filtering
/// cannot be a client-side check inside the resolver: HotChocolate would still emit one response per
/// event to every subscriber regardless of whether the check passed, either surfacing a null payload
/// to a non-matching subscriber or requiring the resolver to swallow events silently, neither of
/// which is how a GraphQL subscription field is meant to behave. Instead every point in the (location,
/// meterType) filter space - none, location only, type only, both - gets its own topic key, computed
/// by the same method on both the publishing consumer and the subscribing resolver, so the two sides
/// can never compute different keys for what is conceptually the same filter.
/// </summary>
public static class ReadingStoredTopics
{
    private const string RootTopic = "OnReadingStored";

    /// <summary>
    /// The key a subscription with the given (possibly absent) filter arguments resolves to. Used
    /// identically by the resolver (to subscribe) and by <see cref="ReadingStoredSubscriptionConsumer"/>
    /// (to publish to every key a real event could match). Upper-invariant so a client's casing of
    /// location/meterType need not match the wire event's exactly, mirroring the case-insensitive
    /// matching <c>AlertRuleEngine.Matches</c> already applies to the same two strings elsewhere in
    /// the system.
    /// </summary>
    public static string Resolve(string? location, string? meterType)
    {
        var key = RootTopic;

        if (location is not null)
        {
            key += $"|location:{location.ToUpperInvariant()}";
        }

        if (meterType is not null)
        {
            key += $"|meterType:{meterType.ToUpperInvariant()}";
        }

        return key;
    }
}
