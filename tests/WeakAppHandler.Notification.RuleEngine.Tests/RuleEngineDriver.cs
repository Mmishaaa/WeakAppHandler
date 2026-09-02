namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// Feeds a sequence of observations through the engine for one rule, carrying the evaluation state
/// forward between them the way TASK-029's consumer will carry it through `alert_rule_state` and
/// `alerts`.
/// </summary>
/// <remarks>
/// State is keyed on (meter, metric) - the same composite key as the table - which is what makes the
/// per-meter cooldown scope observable in a unit test: two meters under one rule simply get two
/// entries, so a suppression that leaked across meters would show up as a missing alert here rather
/// than only in an integration test.
/// </remarks>
internal sealed class RuleEngineDriver(RuleDefinition rule)
{
    private readonly Dictionary<(Guid MeterId, string MetricCode), RuleEvaluationState> _states = [];

    public List<RuleDecision> Decisions { get; } = [];

    public IReadOnlyList<RuleDecisionReason> Reasons => Decisions.ConvertAll(decision => decision.Reason);

    public IReadOnlyList<RuleDecisionKind> Kinds => Decisions.ConvertAll(decision => decision.Kind);

    public RuleDecision Feed(MetricObservation observation)
    {
        (Guid, string) key = (observation.MeterId, observation.MetricCode);
        RuleEvaluationState state = _states.TryGetValue(key, out RuleEvaluationState? existing)
            ? existing
            : RuleEvaluationState.Initial;

        RuleDecision decision = AlertRuleEngine.Evaluate(rule, state, observation);

        _states[key] = new RuleEvaluationState
        {
            WasBreaching = decision.IsBreaching,
            HasActiveAlert = decision.Kind switch
            {
                RuleDecisionKind.Raise => true,
                RuleDecisionKind.Resolve => false,
                _ => state.HasActiveAlert,
            },
            LastTriggeredAt = decision.Kind == RuleDecisionKind.Raise
                ? observation.ObservedAt
                : state.LastTriggeredAt,
        };

        Decisions.Add(decision);
        return decision;
    }

    public int CountOf(RuleDecisionKind kind) => Decisions.Count(decision => decision.Kind == kind);
}
