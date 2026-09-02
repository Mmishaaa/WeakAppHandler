using WeakAppHandler.Contracts;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.RuleEngine;

namespace WeakAppHandler.Notification.Api.Alerting;

/// <summary>
/// The boundary between this service's persisted types and the rule engine's pure ones. The engine
/// carries no reference to either EF or Contracts by design (TASK-028), so something has to translate
/// - and doing it in one place keeps the two enums, the two value shapes and the decimal conversion
/// from being re-derived slightly differently at each call site.
/// </summary>
public static class AlertRuleMapping
{
    /// <summary>
    /// Bounds of `readings.value_numeric` / `alert_rules.threshold_numeric`, both `numeric(12,4)`.
    /// A value outside this range cannot have come from the column the Processor wrote it to.
    /// </summary>
    private const decimal MaxStorableNumeric = 99999999.9999m;

    private const int NumericScale = 4;

    public static RuleDefinition ToDefinition(AlertRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new RuleDefinition
        {
            RuleId = rule.Id,
            Location = rule.Location,
            MeterType = rule.MeterType,
            MetricCode = rule.MetricCode,
            Operator = ToRuleOperator(rule.Operator),
            ThresholdNumeric = rule.ThresholdNumeric,
            ThresholdBool = rule.ThresholdBool,
            HysteresisPercent = rule.HysteresisPercent,
            CooldownSeconds = rule.CooldownSeconds,
            IsEnabled = rule.IsEnabled,
        };
    }

    public static MetricObservation ToObservation(ReadingStored reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return new MetricObservation
        {
            MeterId = reading.MeterId,
            Location = reading.Location,
            MeterType = reading.MeterType,
            MetricCode = reading.MetricCode,
            Numeric = ToStorableDecimal(reading.Value.Numeric),
            Boolean = reading.Value.Boolean,

            // The engine measures cooldown from the observation itself rather than from a clock, so
            // `triggered_at` has to be this same instant - a cooldown compared against wall time
            // while it was recorded against observation time would drift by the queue's own latency.
            ObservedAt = reading.ObservedAt,
        };
    }

    /// <summary>
    /// The two operator enums are deliberately separate types - the engine's may not depend on the
    /// persisted one - so a member added to either has to be reconciled here rather than cast across.
    /// </summary>
    public static RuleOperator ToRuleOperator(AlertOperator value) => value switch
    {
        AlertOperator.Gt => RuleOperator.Gt,
        AlertOperator.Gte => RuleOperator.Gte,
        AlertOperator.Lt => RuleOperator.Lt,
        AlertOperator.Lte => RuleOperator.Lte,
        AlertOperator.Eq => RuleOperator.Eq,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown alert operator."),
    };

    /// <summary>
    /// Converts the double carried on the wire back to the decimal the threshold is stored as, at the
    /// scale of the column it came from.
    /// </summary>
    /// <remarks>
    /// A value that is not finite or that no `numeric(12,4)` column could have held is reported as
    /// "no numeric value" rather than converted: `(decimal)double.NaN` throws, and a message that
    /// carries one is malformed, not a reading. Answering null makes every numeric rule report
    /// <see cref="RuleDecisionReason.NotApplicable"/> for it, which leaves the stored breach state
    /// untouched - the safe outcome, and a far better one than faulting the message into the
    /// dead-letter queue on arithmetic.
    /// </remarks>
    private static decimal? ToStorableDecimal(double? value)
    {
        // The range is checked as a double, before the cast rather than after it: converting a
        // double larger than decimal.MaxValue throws OverflowException, so a check on the converted
        // value would never be reached.
        if (value is not double numeric
            || !double.IsFinite(numeric)
            || Math.Abs(numeric) > (double)MaxStorableNumeric)
        {
            return null;
        }

        return Math.Round((decimal)numeric, NumericScale, MidpointRounding.AwayFromZero);
    }
}
