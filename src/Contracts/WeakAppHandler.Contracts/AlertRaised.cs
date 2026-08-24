namespace WeakAppHandler.Contracts;

// Location/MeterType/MetricCode are denormalised so subscribers need no join back into the
// Processor's schema (Notification owns no foreign keys into meters/metrics).
public sealed record AlertRaised(
    Guid AlertId,
    Guid RuleId,
    Guid MeterId,
    string Location,
    string MeterType,
    string MetricCode,
    string Severity,
    MetricValue TriggeredValue,
    DateTimeOffset TriggeredAt);
