namespace WeakAppHandler.Notification.RuleEngine;

/// <summary>
/// PRD §6.6's evaluation semantics as a pure function: transition triggering, hysteresis, cooldown
/// per (rule, meter, metric), and explicit resolution.
/// </summary>
/// <remarks>
/// This type reads no clock, touches no database and resolves nothing from a container - every input
/// arrives as an argument and the whole result is the return value. That is what makes the boundary
/// cases in PRD §6.6's acceptance criteria (a value exactly on the threshold, a value exactly on the
/// hysteresis edge) testable as arithmetic rather than as an integration scenario, and it is why the
/// project itself is kept free of package and project references.
/// </remarks>
public static class AlertRuleEngine
{
    /// <summary>
    /// Whether <paramref name="rule"/> is in scope for <paramref name="observation"/>: enabled, same
    /// metric, and either unscoped ("any") or scoped to this observation's location and meter type.
    /// </summary>
    /// <remarks>
    /// Comparison is case-insensitive. WeakApp's location and type strings pass through several hands
    /// before they reach a rule - the wire payload, the Processor's meter registration, an operator
    /// typing "garage" into the TASK-030 rule form - and a rule that silently matches nothing because
    /// of a capital letter is a missing alert, the failure mode that is hardest to notice.
    /// </remarks>
    public static bool Matches(RuleDefinition rule, MetricObservation observation)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(observation);

        return rule.IsEnabled
            && string.Equals(rule.MetricCode, observation.MetricCode, StringComparison.OrdinalIgnoreCase)
            && (rule.Location is null || string.Equals(rule.Location, observation.Location, StringComparison.OrdinalIgnoreCase))
            && (rule.MeterType is null || string.Equals(rule.MeterType, observation.MeterType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Evaluates one in-scope rule against one observation, given the state that pairing left behind
    /// last time.
    /// </summary>
    /// <remarks>
    /// Follows PRD §6.6's pseudocode, with two deliberate differences. First, `wasBreaching` is read
    /// from <paramref name="state"/> rather than recomputed from the event's previous value, so a
    /// restart cannot re-raise an alert for a window that is already open. Second, a transition that
    /// is swallowed by cooldown still records the breach: the transition has been consumed, so the
    /// following observations above the threshold stay quiet instead of firing the moment the cooldown
    /// window closes, which would turn cooldown from a rate limit into a delay.
    /// </remarks>
    public static RuleDecision Evaluate(RuleDefinition rule, RuleEvaluationState state, MetricObservation observation)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(observation);

        bool? breaching = IsBreaching(rule, observation);
        if (breaching is null)
        {
            return Decide(RuleDecisionKind.None, RuleDecisionReason.NotApplicable, state.WasBreaching);
        }

        if (breaching.Value)
        {
            RuleDecisionReason breachReason = (state.HasActiveAlert, state.WasBreaching, IsCooldownElapsed(rule, state, observation)) switch
            {
                (true, _, _) => RuleDecisionReason.AlertAlreadyActive,
                (false, true, _) => RuleDecisionReason.NoTransition,
                (false, false, false) => RuleDecisionReason.CooldownActive,
                (false, false, true) => RuleDecisionReason.Raised,
            };

            RuleDecisionKind breachKind = breachReason == RuleDecisionReason.Raised
                ? RuleDecisionKind.Raise
                : RuleDecisionKind.None;

            return Decide(breachKind, breachReason, isBreaching: true);
        }

        if (!state.HasActiveAlert)
        {
            return Decide(RuleDecisionKind.None, RuleDecisionReason.NotBreaching, isBreaching: false);
        }

        return HasClearedHysteresisBand(rule, observation)
            ? Decide(RuleDecisionKind.Resolve, RuleDecisionReason.Resolved, isBreaching: false)
            : Decide(RuleDecisionKind.None, RuleDecisionReason.WithinHysteresisBand, isBreaching: false);
    }

    private static RuleDecision Decide(RuleDecisionKind kind, RuleDecisionReason reason, bool isBreaching) =>
        new() { Kind = kind, Reason = reason, IsBreaching = isBreaching };

    /// <summary>
    /// Compares the observed value against the rule's threshold. Null means the pair cannot be
    /// compared at all, which is a different answer from "not breaching" - it must not clear an open
    /// alert or overwrite the stored breach flag.
    /// </summary>
    private static bool? IsBreaching(RuleDefinition rule, MetricObservation observation)
    {
        // A rule with both thresholds set, or neither, is rejected by a check constraint on
        // alert_rules; the engine still refuses to guess, because such a rule reaching it means the
        // constraint was bypassed and inventing a comparison would hide that.
        if (rule.ThresholdNumeric.HasValue == rule.ThresholdBool.HasValue)
        {
            return null;
        }

        if (rule.ThresholdBool is bool thresholdBool)
        {
            // Ordering operators have no meaning for a boolean metric: `false < true` happens to be
            // true in CLR terms and would make a `lt` rule on motion fire on every quiet poll.
            return observation.Boolean is bool observedBool && rule.Operator == RuleOperator.Eq
                ? observedBool == thresholdBool
                : null;
        }

        if (observation.Numeric is not decimal observedNumeric)
        {
            return null;
        }

        decimal threshold = rule.ThresholdNumeric!.Value;

        return rule.Operator switch
        {
            RuleOperator.Gt => observedNumeric > threshold,
            RuleOperator.Gte => observedNumeric >= threshold,
            RuleOperator.Lt => observedNumeric < threshold,
            RuleOperator.Lte => observedNumeric <= threshold,
            RuleOperator.Eq => observedNumeric == threshold,
            _ => null,
        };
    }

    /// <summary>
    /// Whether a non-breaching value has retreated far enough past the threshold to clear an open
    /// alert (PRD §6.6: "the threshold minus the hysteresis margin").
    /// </summary>
    /// <remarks>
    /// The margin is a percentage of the threshold's magnitude, and the direction it is applied in
    /// follows the operator: an upper bound (`gt`, `gte`) clears downwards at `threshold - margin`, a
    /// lower bound (`lt`, `lte`) clears upwards at `threshold + margin`. Getting that sign wrong on
    /// the `lt` family is the classic version of this bug - the band would sit on the breaching side
    /// of the threshold, so the alert would resolve the instant it stopped breaching and hysteresis
    /// would silently do nothing. A boolean rule has no band at all: it clears as soon as the value
    /// stops matching. So does any rule with a zero margin, and a threshold of zero has a zero margin
    /// by construction (a percentage of nothing is nothing).
    /// </remarks>
    private static bool HasClearedHysteresisBand(RuleDefinition rule, MetricObservation observation)
    {
        if (rule.ThresholdNumeric is not decimal threshold || observation.Numeric is not decimal observed)
        {
            return true;
        }

        decimal margin = Math.Abs(threshold) * Math.Max(0m, rule.HysteresisPercent) / 100m;

        return rule.Operator switch
        {
            RuleOperator.Gt or RuleOperator.Gte => observed <= threshold - margin,
            RuleOperator.Lt or RuleOperator.Lte => observed >= threshold + margin,

            // An `eq` rule breaches on a single point, so its band is symmetric around it: the value
            // has to be at least a margin away on either side before the alert clears.
            _ => Math.Abs(observed - threshold) >= margin,
        };
    }

    private static bool IsCooldownElapsed(RuleDefinition rule, RuleEvaluationState state, MetricObservation observation)
    {
        if (state.LastTriggeredAt is not DateTimeOffset lastTriggeredAt)
        {
            return true;
        }

        return observation.ObservedAt - lastTriggeredAt >= TimeSpan.FromSeconds(Math.Max(0, rule.CooldownSeconds));
    }
}
