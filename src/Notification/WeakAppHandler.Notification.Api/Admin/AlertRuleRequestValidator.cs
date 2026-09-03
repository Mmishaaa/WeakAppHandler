using FluentValidation;

namespace WeakAppHandler.Notification.Api.Admin;

/// <summary>
/// TASK-030's request-level validation: a valid operator, a non-negative cooldown, hysteresis in a
/// reasonable range, and exactly one threshold kind - the same invariants
/// <see cref="WeakAppHandler.Notification.Api.Persistence.Configurations.AlertRuleConfiguration"/>'s
/// check constraints enforce at the database layer, applied here too so a bad request fails with a
/// field-level 400 instead of a raw Postgres constraint-violation exception.
/// </summary>
public sealed class AlertRuleRequestValidator : AbstractValidator<AlertRuleRequest>
{
    private static readonly string[] ValidOperators = ["gt", "gte", "lt", "lte", "eq"];
    private static readonly string[] ValidSeverities = ["info", "warning", "critical"];

    public AlertRuleRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.MetricCode).NotEmpty().MaximumLength(32);
        RuleFor(r => r.Location).MaximumLength(64);
        RuleFor(r => r.MeterType).MaximumLength(32);

        RuleFor(r => r.Operator)
            .Must(op => ValidOperators.Contains(op, StringComparer.Ordinal))
            .WithMessage($"Operator must be one of: {string.Join(", ", ValidOperators)}.");

        RuleFor(r => r.Severity)
            .Must(severity => ValidSeverities.Contains(severity, StringComparer.Ordinal))
            .WithMessage($"Severity must be one of: {string.Join(", ", ValidSeverities)}.");

        // Nullable comparison validators pass a null value through as valid by design - null means
        // "use the entity default", checked only once it is actually supplied.
        RuleFor(r => r.CooldownSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Cooldown must not be negative.");

        RuleFor(r => r.HysteresisPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Hysteresis must be between 0 and 100.");

        // RuleFor targets ThresholdNumeric itself (rather than the whole request via RuleFor(r => r))
        // so the failure's PropertyName is actually "ThresholdNumeric" - WithName/WithMessage only
        // change the rendered message, not the PropertyName ToValidationProblem keys ModelState by.
        RuleFor(r => r.ThresholdNumeric)
            .Must((r, _) => r.ThresholdNumeric.HasValue ^ r.ThresholdBool.HasValue)
            .WithMessage("Exactly one of thresholdNumeric or thresholdBool must be provided.");
    }
}
