namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// PRD §6.6 semantic 2: a breach clears only once the value retreats past the threshold by the
/// configured margin, so a value oscillating around the threshold cannot produce a trigger/resolve
/// cycle. The band edge is asserted exactly, from both sides.
/// </summary>
public sealed class HysteresisTests
{
    [Fact]
    public void Evaluate_ValueOscillatingInsideTheBand_ProducesNoFurtherAlertsOrResolutions()
    {
        // Threshold 1000 with a 5% margin puts the clearing point at 950: every value below is inside
        // the band. Without hysteresis this sequence would be raise/resolve five times over.
        RuleEngineDriver driver = new(SampleRules.Numeric(RuleOperator.Gt, 1000m));

        foreach ((decimal value, int second) in new[] { (1100m, 0), (990m, 10), (1010m, 20), (960m, 30), (1200m, 40), (951m, 50), (1005m, 60) })
        {
            driver.Feed(SampleReadings.Numeric(value, second));
        }

        Assert.Equal(1, driver.CountOf(RuleDecisionKind.Raise));
        Assert.Equal(0, driver.CountOf(RuleDecisionKind.Resolve));
        Assert.Equal(3, driver.Reasons.Count(reason => reason == RuleDecisionReason.WithinHysteresisBand));
        Assert.Equal(3, driver.Reasons.Count(reason => reason == RuleDecisionReason.AlertAlreadyActive));
    }

    [Theory]
    [InlineData(950.0, true)]
    [InlineData(949.9999, true)]
    [InlineData(950.0001, false)]
    [InlineData(999.9999, false)]
    public void Evaluate_UpperBoundRuleAtTheHysteresisEdge_ResolvesOnlyOnceThePointIsReached(
        double value,
        bool expectedResolve)
    {
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m),
            breaching,
            SampleReadings.Numeric((decimal)value));

        Assert.Equal(expectedResolve ? RuleDecisionKind.Resolve : RuleDecisionKind.None, decision.Kind);
        Assert.Equal(
            expectedResolve ? RuleDecisionReason.Resolved : RuleDecisionReason.WithinHysteresisBand,
            decision.Reason);
        Assert.False(decision.IsBreaching);
    }

    [Theory]
    [InlineData(21.0, true)]
    [InlineData(21.0001, true)]
    [InlineData(20.9999, false)]
    [InlineData(20.0, false)]
    public void Evaluate_LowerBoundRuleAtTheHysteresisEdge_AppliesTheBandAboveTheThreshold(
        double value,
        bool expectedResolve)
    {
        // A `lt 20` rule breaches downwards, so its band sits ABOVE the threshold: 20 + 5% of 20 = 21.
        // With the sign inverted the band would land on the breaching side and hysteresis would do
        // nothing at all - the alert would clear the instant the value stopped breaching.
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Lt, 20m),
            breaching,
            SampleReadings.Numeric((decimal)value));

        Assert.Equal(expectedResolve ? RuleDecisionKind.Resolve : RuleDecisionKind.None, decision.Kind);
    }

    [Fact]
    public void Evaluate_LowerBoundRuleOscillatingInsideTheBand_KeepsTheAlertOpen()
    {
        RuleEngineDriver driver = new(SampleRules.Numeric(RuleOperator.Lte, 20m, metricCode: "humidity"));

        foreach ((decimal value, int second) in new[] { (18m, 0), (20.5m, 10), (19m, 20), (20.9m, 30), (21m, 40) })
        {
            driver.Feed(SampleReadings.Numeric(value, second, metricCode: "humidity"));
        }

        Assert.Equal(1, driver.CountOf(RuleDecisionKind.Raise));
        Assert.Equal(1, driver.CountOf(RuleDecisionKind.Resolve));
        Assert.Equal(RuleDecisionReason.Resolved, driver.Reasons[^1]);
    }

    [Fact]
    public void Evaluate_ZeroHysteresis_ResolvesAsSoonAsTheValueStopsBreaching()
    {
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m, hysteresisPercent: 0m),
            breaching,
            SampleReadings.Numeric(1000m));

        Assert.Equal(RuleDecisionKind.Resolve, decision.Kind);
    }

    [Fact]
    public void Evaluate_ZeroThreshold_HasNoBandAndStillResolves()
    {
        // A percentage of zero is zero, so a `gt 0` rule cannot have a band no matter what margin is
        // configured. Worth pinning down: the alternative reading (margin relative to the value) would
        // make this rule unresolvable.
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 0m, hysteresisPercent: 50m),
            breaching,
            SampleReadings.Numeric(0m));

        Assert.Equal(RuleDecisionKind.Resolve, decision.Kind);
    }

    [Theory]
    [InlineData(105.0, true)]
    [InlineData(95.0, true)]
    [InlineData(102.0, false)]
    [InlineData(98.0, false)]
    public void Evaluate_EqualityRuleBand_IsSymmetricAroundTheThreshold(double value, bool expectedResolve)
    {
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Eq, 100m),
            breaching,
            SampleReadings.Numeric((decimal)value));

        Assert.Equal(expectedResolve ? RuleDecisionKind.Resolve : RuleDecisionKind.None, decision.Kind);
    }

    [Fact]
    public void Evaluate_NegativeThreshold_UsesTheMagnitudeForTheMargin()
    {
        // `lt -10` with a 5% margin clears at -10 + 0.5 = -9.5. Taking the margin from the signed
        // threshold instead of its magnitude would flip the band's direction for negative thresholds.
        RuleEvaluationState breaching = new() { WasBreaching = true, HasActiveAlert = true };
        RuleDefinition rule = SampleRules.Numeric(RuleOperator.Lt, -10m);

        Assert.Equal(
            RuleDecisionKind.None,
            AlertRuleEngine.Evaluate(rule, breaching, SampleReadings.Numeric(-9.6m)).Kind);
        Assert.Equal(
            RuleDecisionKind.Resolve,
            AlertRuleEngine.Evaluate(rule, breaching, SampleReadings.Numeric(-9.5m)).Kind);
    }
}
