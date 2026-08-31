namespace WeakAppHandler.Processor.Application.Ingestion;

/// <summary>
/// One payload field flattened to a <c>readings.metric_code</c> and the value it carried. Exactly
/// one of <paramref name="ValueNumeric"/>/<paramref name="ValueBool"/> is set, mirroring the
/// <c>value_numeric</c>/<c>value_bool</c> column pair every metric row is stored with.
/// </summary>
public sealed record NormalizedMetricValue(string MetricCode, decimal? ValueNumeric, bool? ValueBool);
