namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// Rule scoping (PRD §6.6): a rule applies to a metric, optionally narrowed to a location and a meter
/// type, where null means "any".
/// </summary>
public sealed class RuleMatchingTests
{
    [Fact]
    public void Matches_UnscopedRule_AppliesToEveryLocationAndMeterType()
    {
        RuleDefinition rule = SampleRules.Numeric(RuleOperator.Gt, 1000m);

        Assert.True(AlertRuleEngine.Matches(rule, SampleReadings.Numeric(1m, location: "Kitchen")));
        Assert.True(AlertRuleEngine.Matches(rule, SampleReadings.Numeric(1m, location: "Garage")));
    }

    [Fact]
    public void Matches_ScopedRule_IgnoresOtherLocations()
    {
        RuleDefinition garageOnly = SampleRules.Boolean();

        Assert.True(AlertRuleEngine.Matches(garageOnly, SampleReadings.Boolean(true, location: "Garage")));
        Assert.False(AlertRuleEngine.Matches(garageOnly, SampleReadings.Boolean(true, location: "Kitchen")));
    }

    [Fact]
    public void Matches_DifferingCase_StillMatches()
    {
        // Location and type strings pass through the wire payload, the Processor's meter registration
        // and an operator's typing before they meet a rule. A rule that matches nothing because of a
        // capital letter produces no alerts and no errors either.
        RuleDefinition garageOnly = SampleRules.Boolean();
        MetricObservation lowerCased = SampleReadings.Boolean(true, location: "garage") with
        {
            MeterType = "MOTION",
            MetricCode = "Motion_Detected",
        };

        Assert.True(AlertRuleEngine.Matches(garageOnly, lowerCased));
    }

    [Fact]
    public void Matches_DifferentMetric_DoesNotMatch()
    {
        RuleDefinition co2Rule = SampleRules.Numeric(RuleOperator.Gt, 1000m);

        Assert.False(AlertRuleEngine.Matches(co2Rule, SampleReadings.Numeric(1m, metricCode: "pm25")));
    }

    [Fact]
    public void Matches_DifferentMeterType_DoesNotMatch()
    {
        RuleDefinition motionMetersOnly = SampleRules.Numeric(RuleOperator.Gt, 1000m) with { MeterType = "motion" };

        Assert.False(AlertRuleEngine.Matches(motionMetersOnly, SampleReadings.Numeric(1m)));
    }

    [Fact]
    public void Matches_DisabledRule_NeverMatches()
    {
        RuleDefinition disabled = SampleRules.Numeric(RuleOperator.Gt, 1000m) with { IsEnabled = false };

        Assert.False(AlertRuleEngine.Matches(disabled, SampleReadings.Numeric(1200m)));
    }
}
