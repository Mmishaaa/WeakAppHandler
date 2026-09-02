namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// PRD §6.6 semantic 3 plus the architecture review's correction to it: cooldown is a minimum
/// interval between alerts for one (rule, meter, metric), not one interval shared by the whole rule.
/// </summary>
public sealed class CooldownTests
{
    private const int FiveMinutes = 300;

    [Fact]
    public void Evaluate_TransitionInsideTheCooldownWindow_IsSuppressed()
    {
        RuleEvaluationState justTriggered = new()
        {
            WasBreaching = false,
            HasActiveAlert = false,
            LastTriggeredAt = SampleReadings.Start,
        };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m, cooldownSeconds: FiveMinutes),
            justTriggered,
            SampleReadings.Numeric(1200m, 60));

        Assert.Equal(RuleDecisionKind.None, decision.Kind);
        Assert.Equal(RuleDecisionReason.CooldownActive, decision.Reason);
        Assert.True(decision.IsBreaching);
    }

    [Theory]
    [InlineData(299, false)]
    [InlineData(300, true)]
    [InlineData(301, true)]
    public void Evaluate_TransitionExactlyAtTheCooldownBoundary_RaisesOnceTheWindowHasElapsed(
        int secondsSinceLastAlert,
        bool expectedRaise)
    {
        RuleEvaluationState justTriggered = new()
        {
            WasBreaching = false,
            HasActiveAlert = false,
            LastTriggeredAt = SampleReadings.Start,
        };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m, cooldownSeconds: FiveMinutes),
            justTriggered,
            SampleReadings.Numeric(1200m, secondsSinceLastAlert));

        Assert.Equal(expectedRaise ? RuleDecisionKind.Raise : RuleDecisionKind.None, decision.Kind);
    }

    [Fact]
    public void Evaluate_BreachSwallowedByCooldown_DoesNotFireLateOnTheNextReading()
    {
        // The whole point of the swallowed transition recording its breach: the alert is dropped, not
        // queued. A deferred version of this would fire the moment the window closed, which turns a
        // rate limit into a delayed duplicate.
        RuleEngineDriver driver = new(SampleRules.Numeric(RuleOperator.Gt, 1000m, cooldownSeconds: FiveMinutes));

        driver.Feed(SampleReadings.Numeric(1200m, 0));
        driver.Feed(SampleReadings.Numeric(900m, 10));
        driver.Feed(SampleReadings.Numeric(1200m, 60));
        driver.Feed(SampleReadings.Numeric(1300m, 70));
        driver.Feed(SampleReadings.Numeric(1300m, 400));

        RuleDecisionReason[] expected =
        [
            RuleDecisionReason.Raised,
            RuleDecisionReason.Resolved,
            RuleDecisionReason.CooldownActive,
            RuleDecisionReason.NoTransition,
            RuleDecisionReason.NoTransition,
        ];

        Assert.Equal(expected, driver.Reasons);
        Assert.Equal(1, driver.CountOf(RuleDecisionKind.Raise));
    }

    [Fact]
    public void Evaluate_NewTransitionAfterTheCooldownWindow_RaisesAgain()
    {
        RuleEngineDriver driver = new(SampleRules.Numeric(RuleOperator.Gt, 1000m, cooldownSeconds: FiveMinutes));

        driver.Feed(SampleReadings.Numeric(1200m, 0));
        driver.Feed(SampleReadings.Numeric(900m, 10));
        driver.Feed(SampleReadings.Numeric(1200m, 400));

        Assert.Equal(2, driver.CountOf(RuleDecisionKind.Raise));
    }

    [Fact]
    public void Evaluate_CooldownFromAnotherMeter_DoesNotSuppressThisOne()
    {
        // The failure this table's composite key exists to prevent: one shared last_triggered_at on
        // the rule would let CO2 in the kitchen silence CO2 in the garage for five minutes, and a
        // silenced alert looks exactly like a healthy room.
        RuleEngineDriver driver = new(SampleRules.Numeric(RuleOperator.Gt, 1000m, cooldownSeconds: FiveMinutes));

        driver.Feed(SampleReadings.Numeric(1200m, 0, SampleReadings.MeterA));
        driver.Feed(SampleReadings.Numeric(1200m, 5, SampleReadings.MeterB, location: "Garage"));

        Assert.Equal(2, driver.CountOf(RuleDecisionKind.Raise));
        Assert.All(driver.Decisions, decision => Assert.Equal(RuleDecisionReason.Raised, decision.Reason));
    }

    [Fact]
    public void Evaluate_NeverTriggeredBefore_IgnoresCooldownEntirely()
    {
        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m, cooldownSeconds: 86_400),
            RuleEvaluationState.Initial,
            SampleReadings.Numeric(1200m));

        Assert.Equal(RuleDecisionKind.Raise, decision.Kind);
    }

    [Fact]
    public void Evaluate_NegativeCooldown_IsTreatedAsNoCooldown()
    {
        RuleEvaluationState justTriggered = new()
        {
            WasBreaching = false,
            HasActiveAlert = false,
            LastTriggeredAt = SampleReadings.Start,
        };

        RuleDecision decision = AlertRuleEngine.Evaluate(
            SampleRules.Numeric(RuleOperator.Gt, 1000m, cooldownSeconds: -60),
            justTriggered,
            SampleReadings.Numeric(1200m));

        Assert.Equal(RuleDecisionKind.Raise, decision.Kind);
    }
}
