namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// PRD §6.6 semantics 1 and 4: an alert is raised when a value crosses a threshold, not on every
/// reading beyond it, and resolution is explicit.
/// </summary>
public sealed class TransitionTriggeringTests
{
    [Fact]
    public void Evaluate_ValueRisesAndStaysAboveThreshold_RaisesExactlyOneAlert()
    {
        RuleEngineDriver driver = new(SampleRules.Numeric(RuleOperator.Gt, 1000m));

        // Eight polls at a ten-second interval, seven of them breaching: level-triggered alerting
        // would produce seven alerts here, which is the failure mode this rule model exists to avoid.
        foreach ((decimal value, int second) in new[] { (900m, 0), (1100m, 10), (1200m, 20), (1300m, 30), (1250m, 40), (1100m, 50), (1400m, 60), (1050m, 70) })
        {
            driver.Feed(SampleReadings.Numeric(value, second));
        }

        RuleDecisionReason[] expected =
        [
            RuleDecisionReason.NotBreaching,
            RuleDecisionReason.Raised,
            RuleDecisionReason.AlertAlreadyActive,
            RuleDecisionReason.AlertAlreadyActive,
            RuleDecisionReason.AlertAlreadyActive,
            RuleDecisionReason.AlertAlreadyActive,
            RuleDecisionReason.AlertAlreadyActive,
            RuleDecisionReason.AlertAlreadyActive,
        ];

        Assert.Equal(1, driver.CountOf(RuleDecisionKind.Raise));
        Assert.Equal(RuleDecisionKind.Raise, driver.Kinds[1]);
        Assert.Equal(expected, driver.Reasons);
    }

    [Fact]
    public void Evaluate_ValueExactlyAtThreshold_DoesNotBreachGreaterThan()
    {
        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m),
            RuleEvaluationState.Initial,
            SampleReadings.Numeric(1000m));

        Assert.Equal(RuleDecisionKind.None, decision.Kind);
        Assert.Equal(RuleDecisionReason.NotBreaching, decision.Reason);
        Assert.False(decision.IsBreaching);
    }

    [Fact]
    public void Evaluate_ValueExactlyAtThreshold_BreachesGreaterThanOrEqual()
    {
        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gte, 1000m),
            RuleEvaluationState.Initial,
            SampleReadings.Numeric(1000m));

        Assert.Equal(RuleDecisionKind.Raise, decision.Kind);
        Assert.True(decision.IsBreaching);
    }

    [Theory]
    [InlineData(RuleOperator.Lt, 19.9999, true)]
    [InlineData(RuleOperator.Lt, 20.0, false)]
    [InlineData(RuleOperator.Lte, 20.0, true)]
    [InlineData(RuleOperator.Lte, 20.0001, false)]
    public void Evaluate_LowerBoundOperatorAtTheThreshold_BreachesOnlyWhenInclusive(
        RuleOperator ruleOperator,
        double value,
        bool expectedBreach)
    {
        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(ruleOperator, 20m),
            RuleEvaluationState.Initial,
            SampleReadings.Numeric((decimal)value));

        Assert.Equal(expectedBreach, decision.IsBreaching);
        Assert.Equal(expectedBreach ? RuleDecisionKind.Raise : RuleDecisionKind.None, decision.Kind);
    }

    [Fact]
    public void Evaluate_StoredBreachFlagWithNoOpenAlert_ReportsNoTransition()
    {
        // The state a cooldown-swallowed breach leaves behind: the transition has been consumed, so
        // the next reading above the threshold must stay quiet rather than fire late.
        RuleEvaluationState afterSwallowedBreach = new() { WasBreaching = true, HasActiveAlert = false };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m),
            afterSwallowedBreach,
            SampleReadings.Numeric(1200m));

        Assert.Equal(RuleDecisionKind.None, decision.Kind);
        Assert.Equal(RuleDecisionReason.NoTransition, decision.Reason);
        Assert.True(decision.IsBreaching);
    }

    [Fact]
    public void Evaluate_ValueRetreatsAfterAlert_ResolvesOnceAndStaysQuiet()
    {
        RuleEngineDriver driver = new(SampleRules.Numeric(RuleOperator.Gt, 1000m));

        driver.Feed(SampleReadings.Numeric(1200m, 0));
        driver.Feed(SampleReadings.Numeric(800m, 10));
        driver.Feed(SampleReadings.Numeric(700m, 20));

        RuleDecisionReason[] expected =
            [RuleDecisionReason.Raised, RuleDecisionReason.Resolved, RuleDecisionReason.NotBreaching];

        Assert.Equal(1, driver.CountOf(RuleDecisionKind.Raise));
        Assert.Equal(1, driver.CountOf(RuleDecisionKind.Resolve));
        Assert.Equal(expected, driver.Reasons);
    }
}
