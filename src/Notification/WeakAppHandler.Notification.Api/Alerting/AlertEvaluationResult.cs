using WeakAppHandler.Contracts;

namespace WeakAppHandler.Notification.Api.Alerting;

/// <summary>
/// What one reading changed: the alerts it raised and the ones it resolved, already committed and
/// ready to be dispatched.
/// </summary>
/// <remarks>
/// One reading can move more than one rule at a time - the seed set alone has two CO2 rules at
/// different thresholds, so a value crossing 1400 raises both the warning and the critical alert -
/// which is why these are lists rather than a single optional alert.
/// </remarks>
public sealed record AlertEvaluationResult(
    IReadOnlyList<AlertRaised> Raised,
    IReadOnlyList<AlertResolved> Resolved)
{
    public static AlertEvaluationResult Empty { get; } = new([], []);
}
