using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WeakAppHandler.Contracts;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence;
using WeakAppHandler.Notification.Api.Persistence.Converters;
using WeakAppHandler.Notification.RuleEngine;

namespace WeakAppHandler.Notification.Api.Alerting;

/// <summary>
/// Applies every rule in scope for one reading and writes what the engine decided: new `alerts` rows,
/// resolutions of open ones, and the per (rule, meter, metric) evaluation state the next reading is
/// judged against (PRD §6.6).
/// </summary>
/// <remarks>
/// <para>
/// The whole reading is one <c>SaveChanges</c>, so an alert and the state that says it was raised
/// cannot end up disagreeing. Nothing is dispatched here: a subscriber told about an alert before the
/// write commits could be told about one that a rollback then erases, so the caller dispatches the
/// returned events afterwards.
/// </para>
/// <para>
/// A redelivery of the same reading is harmless without a `processed_messages` ledger of its own,
/// which is why this service has none: the second evaluation finds `was_breaching` already true and
/// an alert already open, so the engine answers NoTransition/AlertAlreadyActive and nothing is
/// written twice. The partial unique index on active alerts is the backstop for the case the two
/// deliveries overlap instead of following one another.
/// </para>
/// </remarks>
public sealed partial class AlertEvaluator(AlertingDbContext dbContext, ILogger<AlertEvaluator> logger)
{
    public async Task<AlertEvaluationResult> EvaluateAsync(ReadingStored reading, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var observation = AlertRuleMapping.ToObservation(reading);

        // Scoping is decided by AlertRuleEngine.Matches rather than in SQL, so there is exactly one
        // definition of what "this rule applies" means. Filtering on the metric code in the query
        // would need a case-insensitive comparison to agree with the engine, and a case-sensitive
        // one that quietly disagrees produces no alerts and no errors. The rule set is what an
        // operator authored by hand, so the read is small; if it ever stops being small the answer
        // is a cache invalidated by the rule CRUD, not a filter that can drift from the engine.
        var enabledRules = await dbContext.AlertRules
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var applicable = enabledRules
            .Select(rule => (Rule: rule, Definition: AlertRuleMapping.ToDefinition(rule)))
            .Where(candidate => AlertRuleEngine.Matches(candidate.Definition, observation))
            .ToList();

        if (applicable.Count == 0)
        {
            return AlertEvaluationResult.Empty;
        }

        var ruleIds = applicable.Select(candidate => candidate.Rule.Id).ToList();
        var states = await LoadStatesAsync(ruleIds, reading, cancellationToken).ConfigureAwait(false);
        var activeAlerts = await LoadActiveAlertsAsync(ruleIds, reading, cancellationToken).ConfigureAwait(false);

        var raised = new List<AlertRaised>();
        var resolved = new List<AlertResolved>();

        foreach (var (rule, definition) in applicable)
        {
            states.TryGetValue(rule.Id, out var storedState);
            activeAlerts.TryGetValue(rule.Id, out var activeAlert);

            var decision = AlertRuleEngine.Evaluate(
                definition,
                new RuleEvaluationState
                {
                    WasBreaching = storedState?.WasBreaching ?? false,
                    HasActiveAlert = activeAlert is not null,
                    LastTriggeredAt = storedState?.LastTriggeredAt,
                },
                observation);

            switch (decision.Kind)
            {
                case RuleDecisionKind.Raise:
                    raised.Add(Raise(rule, reading, observation));
                    break;

                case RuleDecisionKind.Resolve:
                    // The engine only answers Resolve when the state it was given said an alert was
                    // open, and that flag is this dictionary.
                    resolved.Add(Resolve(activeAlert!, reading, observation));
                    break;

                default:
                    break;
            }

            ApplyState(storedState, rule.Id, decision, observation);
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);

        LogEvaluated(logger, reading.MetricCode, reading.MeterId, applicable.Count, raised.Count, resolved.Count);

        return new AlertEvaluationResult(raised, resolved);
    }

    /// <summary>
    /// Closes an open alert with the value and instant that cleared it. The severity on the event is
    /// the one the alert was raised under, not the rule's current one - the two differ as soon as an
    /// operator edits the rule while the alert is open.
    /// </summary>
    private static AlertResolved Resolve(Alert alert, ReadingStored reading, MetricObservation observation)
    {
        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = observation.ObservedAt;
        alert.ResolvedValueNumeric = observation.Numeric;
        alert.ResolvedValueBool = observation.Boolean;

        return new AlertResolved(
            alert.Id,
            alert.RuleId,
            reading.MeterId,
            reading.Location,
            reading.MeterType,
            reading.MetricCode,
            AlertSeverityConverter.ToCode(alert.Severity),
            reading.Value,
            observation.ObservedAt);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Evaluated {RuleCount} rules for {MetricCode} on meter {MeterId}: raised {RaisedCount}, resolved {ResolvedCount}")]
    private static partial void LogEvaluated(
        ILogger logger,
        string metricCode,
        Guid meterId,
        int ruleCount,
        int raisedCount,
        int resolvedCount);

    private Task<Dictionary<Guid, AlertRuleState>> LoadStatesAsync(
        List<Guid> ruleIds,
        ReadingStored reading,
        CancellationToken cancellationToken) =>
        dbContext.AlertRuleStates
            .Where(s => ruleIds.Contains(s.RuleId)
                && s.MeterId == reading.MeterId
                && s.MetricCode == reading.MetricCode)
            .ToDictionaryAsync(s => s.RuleId, cancellationToken);

    private Task<Dictionary<Guid, Alert>> LoadActiveAlertsAsync(
        List<Guid> ruleIds,
        ReadingStored reading,
        CancellationToken cancellationToken) =>
        dbContext.Alerts
            .Where(a => ruleIds.Contains(a.RuleId)
                && a.MeterId == reading.MeterId
                && a.MetricCode == reading.MetricCode
                && a.Status == AlertStatus.Active)
            .ToDictionaryAsync(a => a.RuleId, cancellationToken);

    private AlertRaised Raise(AlertRule rule, ReadingStored reading, MetricObservation observation)
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            MeterId = reading.MeterId,

            // Copied onto the row rather than joined for at read time: `meters` belongs to the
            // Processor, and the feed has to be servable without reaching into another owner's schema.
            Location = reading.Location,
            MeterType = reading.MeterType,
            MetricCode = reading.MetricCode,
            Status = AlertStatus.Active,

            // The rule's severity as it is right now, so editing the rule later does not rewrite what
            // this alert was raised under.
            Severity = rule.Severity,
            TriggeredAt = observation.ObservedAt,
            TriggeredValueNumeric = observation.Numeric,
            TriggeredValueBool = observation.Boolean,
        };

        dbContext.Alerts.Add(alert);

        // Display-only, for the rule list an operator reads (PRD §7.1). Cooldown is measured from
        // alert_rule_state, never from here - this column knows nothing about which meter fired.
        rule.LastTriggeredAt = observation.ObservedAt;

        return new AlertRaised(
            alert.Id,
            rule.Id,
            reading.MeterId,
            reading.Location,
            reading.MeterType,
            reading.MetricCode,
            AlertSeverityConverter.ToCode(rule.Severity),
            reading.Value,
            observation.ObservedAt);
    }

    /// <summary>
    /// Upserts the (rule, meter, metric) row the next reading will be judged against.
    /// </summary>
    /// <remarks>
    /// An observation the rule cannot be evaluated against writes nothing at all: overwriting
    /// `was_breaching` with a value the engine never computed would fake a transition on the next
    /// reading that actually can be compared.
    /// </remarks>
    private void ApplyState(
        AlertRuleState? storedState,
        Guid ruleId,
        RuleDecision decision,
        MetricObservation observation)
    {
        if (decision.Reason == RuleDecisionReason.NotApplicable)
        {
            return;
        }

        var triggeredAt = decision.Kind == RuleDecisionKind.Raise ? observation.ObservedAt : (DateTimeOffset?)null;

        if (storedState is null)
        {
            dbContext.AlertRuleStates.Add(new AlertRuleState
            {
                RuleId = ruleId,
                MeterId = observation.MeterId,

                // The reading's spelling, not the rule's: this is the key every later lookup for this
                // meter's metric uses, and the rule may be written in a different case.
                MetricCode = observation.MetricCode,
                WasBreaching = decision.IsBreaching,
                LastTriggeredAt = triggeredAt,
                LastEvaluatedAt = observation.ObservedAt,
            });

            return;
        }

        storedState.WasBreaching = decision.IsBreaching;
        storedState.LastEvaluatedAt = observation.ObservedAt;

        if (triggeredAt is not null)
        {
            storedState.LastTriggeredAt = triggeredAt;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Entities that failed to save stay tracked, and MassTransit retries a faulted message on
            // the same scope - a second attempt would then throw on the change tracker instead of on
            // the database, and could never succeed. Cleared here so the retry re-reads the state the
            // delivery that beat us committed, which is what makes it come out as "already active".
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
