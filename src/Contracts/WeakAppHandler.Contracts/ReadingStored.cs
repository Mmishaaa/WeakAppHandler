namespace WeakAppHandler.Contracts;

// Carries everything the Notification service's rule engine needs to evaluate alert rules
// without querying the Processor's database. PreviousValue is null for a meter's first reading.
public sealed record ReadingStored(
    Guid MeterId,
    string Location,
    string MeterType,
    string MetricCode,
    MetricValue Value,
    MetricValue? PreviousValue,
    bool IsChanged,
    DateTimeOffset ObservedAt);
