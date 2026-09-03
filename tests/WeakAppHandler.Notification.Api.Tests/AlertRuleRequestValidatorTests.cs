using WeakAppHandler.Notification.Api.Admin;

namespace WeakAppHandler.Notification.Api.Tests;

/// <summary>
/// TASK-030's request validation in isolation, no host involved: <see cref="AlertRuleRequestValidator"/>
/// is a pure FluentValidation rule set, so its own correctness is a unit test. The 400-with-field-level-
/// message behaviour it feeds into is <see cref="AlertRulesAdminEndpointsTests"/>'s job.
/// </summary>
public sealed class AlertRuleRequestValidatorTests
{
    private readonly AlertRuleRequestValidator _validator = new();

    [Fact]
    public void Validate_AWellFormedRequest_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest());

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("gt")]
    [InlineData("gte")]
    [InlineData("lt")]
    [InlineData("lte")]
    [InlineData("eq")]
    public void Validate_EachDocumentedOperatorCode_IsAccepted(string op)
    {
        var result = _validator.Validate(ValidRequest() with { Operator = op });

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_AnUndocumentedOperator_FailsOnTheOperatorField()
    {
        var result = _validator.Validate(ValidRequest() with { Operator = "greater-than" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AlertRuleRequest.Operator));
    }

    [Fact]
    public void Validate_AnUndocumentedSeverity_FailsOnTheSeverityField()
    {
        var result = _validator.Validate(ValidRequest() with { Severity = "urgent" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AlertRuleRequest.Severity));
    }

    [Fact]
    public void Validate_ANegativeCooldown_FailsOnTheCooldownField()
    {
        var result = _validator.Validate(ValidRequest() with { CooldownSeconds = -1 });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AlertRuleRequest.CooldownSeconds));
    }

    [Fact]
    public void Validate_AZeroCooldown_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest() with { CooldownSeconds = 0 });

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_ANullCooldown_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest() with { CooldownSeconds = null });

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Validate_HysteresisOutsideZeroToOneHundred_FailsOnTheHysteresisField(decimal hysteresis)
    {
        var result = _validator.Validate(ValidRequest() with { HysteresisPercent = hysteresis });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AlertRuleRequest.HysteresisPercent));
    }

    [Fact]
    public void Validate_NeitherThresholdKindProvided_FailsOnTheThresholdField()
    {
        var result = _validator.Validate(ValidRequest() with { ThresholdNumeric = null, ThresholdBool = null });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AlertRuleRequest.ThresholdNumeric));
    }

    [Fact]
    public void Validate_BothThresholdKindsProvided_FailsOnTheThresholdField()
    {
        var result = _validator.Validate(ValidRequest() with { ThresholdBool = true });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AlertRuleRequest.ThresholdNumeric));
    }

    [Fact]
    public void Validate_EmptyName_FailsOnTheNameField()
    {
        var result = _validator.Validate(ValidRequest() with { Name = string.Empty });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AlertRuleRequest.Name));
    }

    private static AlertRuleRequest ValidRequest() => new(
        Name: "Test rule",
        Location: null,
        MeterType: null,
        MetricCode: "co2",
        Operator: "gt",
        ThresholdNumeric: 1000m,
        ThresholdBool: null,
        Severity: "warning",
        HysteresisPercent: null,
        CooldownSeconds: null,
        IsEnabled: null);
}
