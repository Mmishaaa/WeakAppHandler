namespace WeakAppHandler.Contracts;

// Exactly one of Numeric/Boolean is set, mirroring the value_numeric/value_bool column pair.
public sealed record MetricValue(double? Numeric, bool? Boolean);
