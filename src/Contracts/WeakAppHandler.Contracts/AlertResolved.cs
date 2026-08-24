namespace WeakAppHandler.Contracts;

public sealed record AlertResolved(
    Guid AlertId,
    Guid RuleId,
    Guid MeterId,
    string Location,
    string MeterType,
    string MetricCode,
    string Severity,
    MetricValue ResolvedValue,
    DateTimeOffset ResolvedAt);
