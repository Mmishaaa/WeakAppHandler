using WeakAppHandler.Gateway.Application.Alerting;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence;

public sealed class GatewayAlertingReadContext(GatewayAlertingReadDbContext dbContext) : IGatewayAlertingReadContext
{
    public IQueryable<AlertReadModel> Alerts => dbContext.Alerts
        .Select(a => new AlertReadModel
        {
            Id = a.Id,
            RuleId = a.RuleId,
            MeterId = a.MeterId,
            Location = a.Location,
            MeterType = a.MeterType,
            MetricCode = a.MetricCode,
            Status = a.Status,
            Severity = a.Severity,
            TriggeredAt = a.TriggeredAt,
            TriggeredValueNumeric = a.TriggeredValueNumeric,
            TriggeredValueBool = a.TriggeredValueBool,
            ResolvedAt = a.ResolvedAt,
            ResolvedValueNumeric = a.ResolvedValueNumeric,
            ResolvedValueBool = a.ResolvedValueBool,
        });

    public IQueryable<AlertRuleReadModel> AlertRules => dbContext.AlertRules
        .Select(r => new AlertRuleReadModel
        {
            Id = r.Id,
            Name = r.Name,
            Location = r.Location,
            MeterType = r.MeterType,
            MetricCode = r.MetricCode,
            Operator = r.Operator,
            ThresholdNumeric = r.ThresholdNumeric,
            ThresholdBool = r.ThresholdBool,
            Severity = r.Severity,
            HysteresisPercent = r.HysteresisPercent,
            CooldownSeconds = r.CooldownSeconds,
            IsEnabled = r.IsEnabled,
            LastTriggeredAt = r.LastTriggeredAt,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
        });
}
