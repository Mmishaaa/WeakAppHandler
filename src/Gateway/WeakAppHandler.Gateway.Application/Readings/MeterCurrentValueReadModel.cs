namespace WeakAppHandler.Gateway.Application.Readings;

/// <summary>One row of meter_current_state, projected for the "current value" field on a meter.</summary>
public sealed record MeterCurrentValueReadModel(
    Guid MeterId,
    string MetricCode,
    decimal? ValueNumeric,
    bool? ValueBool,
    decimal? PreviousValueNumeric,
    bool? PreviousValueBool,
    DateTimeOffset ObservedAt,
    DateTimeOffset ChangedAt);
