namespace WeakAppHandler.Gateway.Api.GraphQL;

/// <summary>
/// Query-shape limits enforced on every request (TASK-025), independent of authentication - the
/// Gateway has no per-caller rate limiting yet, so these are what stand between an accidentally or
/// deliberately pathological query and the database behind it.
/// </summary>
public static class GraphQLSecurityLimits
{
    /// <summary>
    /// Nothing this schema exposes needs more than a handful of levels: the deepest legitimate
    /// selection today is <c>meters { currentValues { ... } }</c> or a paginated
    /// <c>readings { nodes { ... } }</c>, both depth 3 from the root. Left with generous headroom
    /// for fields TASK-024/026/032 add, while still rejecting the classic introspection recursion
    /// (<c>__type { fields { type { fields { type { ... } } } } }</c>) long before it reaches
    /// anything expensive.
    /// </summary>
    public const int MaxExecutionDepth = 12;
}
