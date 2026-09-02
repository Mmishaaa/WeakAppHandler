namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// The seed rule set includes a boolean rule (motion in the Garage), and a boolean metric breaks two
/// assumptions the numeric path is built on: there is no band to retreat through, and the ordering
/// operators mean nothing.
/// </summary>
public sealed class BooleanRuleTests
{
    [Fact]
    public void Evaluate_MotionGoesTrueThenFalse_RaisesOnceAndResolvesOnce()
    {
        RuleEngineDriver driver = new(SampleRules.Boolean());

        foreach ((bool value, int second) in new[] { (false, 0), (true, 10), (true, 20), (true, 30), (false, 40), (false, 50) })
        {
            driver.Feed(SampleReadings.Boolean(value, second));
        }

        RuleDecisionReason[] expected =
        [
            RuleDecisionReason.NotBreaching,
            RuleDecisionReason.Raised,
            RuleDecisionReason.AlertAlreadyActive,
            RuleDecisionReason.AlertAlreadyActive,
            RuleDecisionReason.Resolved,
            RuleDecisionReason.NotBreaching,
        ];

        Assert.Equal(expected, driver.Reasons);
    }

    [Fact]
    public void Evaluate_BooleanRuleWithNonZeroHysteresis_StillResolvesOnTheValueFlipping()
    {
        // A margin on a boolean rule is meaningless rather than harmful: there is nothing between
        // true and false to retreat through, so it must not be able to trap an alert open forever.
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Boolean(hysteresisPercent: 90m),
            breaching,
            SampleReadings.Boolean(false));

        Assert.Equal(RuleDecisionKind.Resolve, decision.Kind);
    }

    [Fact]
    public void Evaluate_BooleanRuleOnFalse_RaisesWhenTheThresholdIsFalse()
    {
        // `eq false` is a legitimate rule (a sensor that should always read true), and it must not be
        // confused with "not breaching".
        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Boolean(threshold: false),
            RuleEvaluationState.Initial,
            SampleReadings.Boolean(false));

        Assert.Equal(RuleDecisionKind.Raise, decision.Kind);
        Assert.True(decision.IsBreaching);
    }

    [Theory]
    [InlineData(RuleOperator.Gt)]
    [InlineData(RuleOperator.Gte)]
    [InlineData(RuleOperator.Lt)]
    [InlineData(RuleOperator.Lte)]
    public void Evaluate_OrderingOperatorOnABooleanRule_IsNotApplicableRatherThanComparingBooleans(
        RuleOperator ruleOperator)
    {
        // `false < true` is true in CLR terms, so a naive comparison would make an `lt true` rule
        // breach on every quiet poll - an alert storm from a rule that should never have evaluated.
        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Boolean(ruleOperator: ruleOperator),
            RuleEvaluationState.Initial,
            SampleReadings.Boolean(false));

        Assert.Equal(RuleDecisionKind.None, decision.Kind);
        Assert.Equal(RuleDecisionReason.NotApplicable, decision.Reason);
    }

    [Fact]
    public void Evaluate_NumericRuleAgainstABooleanReading_IsNotApplicableAndLeavesTheStateAlone()
    {
        // An inapplicable evaluation is not the same answer as "not breaching": clearing the flag
        // here would fake a transition on the next reading that can be compared.
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m),
            breaching,
            SampleReadings.Boolean(true));

        Assert.Equal(RuleDecisionKind.None, decision.Kind);
        Assert.Equal(RuleDecisionReason.NotApplicable, decision.Reason);
        Assert.True(decision.IsBreaching);
    }

    [Fact]
    public void Evaluate_BooleanRuleAgainstANumericReading_IsNotApplicable()
    {
        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Boolean(),
            RuleEvaluationState.Initial,
            SampleReadings.Numeric(1m));

        Assert.Equal(RuleDecisionReason.NotApplicable, decision.Reason);
    }

    [Fact]
    public void Evaluate_RuleWithNoThreshold_IsNotApplicable()
    {
        // A rule with neither threshold is rejected by a check constraint on alert_rules; if one ever
        // reaches the engine, the symptom must be a logged NotApplicable rather than a guess.
        RuleDefinition thresholdless = SampleRules.Numeric(RuleOperator.Gt, 0m) with { ThresholdNumeric = null };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            thresholdless,
            RuleEvaluationState.Initial,
            SampleReadings.Numeric(1200m));

        Assert.Equal(RuleDecisionReason.NotApplicable, decision.Reason);
    }

    [Fact]
    public void Evaluate_RuleWithBothThresholds_IsNotApplicable()
    {
        RuleDefinition ambiguous = SampleRules.Numeric(RuleOperator.Eq, 1m) with { ThresholdBool = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            ambiguous,
            RuleEvaluationState.Initial,
            SampleReadings.Numeric(1m));

        Assert.Equal(RuleDecisionReason.NotApplicable, decision.Reason);
    }
}
