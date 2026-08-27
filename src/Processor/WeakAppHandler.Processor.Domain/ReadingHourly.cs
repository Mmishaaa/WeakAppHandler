namespace WeakAppHandler.Processor.Domain;

// Target table for the retention job's hourly rollup (TASK-048): raw `readings` rows older than
// the retention window are aggregated into one row per (meter, metric, hour) here and then
// deleted. No writer exists yet - this task only creates the schema.
public sealed class ReadingHourly
{
    public long Id { get; init; }

    public required Guid MeterId { get; init; }

    public required string MetricCode { get; init; }

    public required DateTimeOffset BucketStart { get; init; }

    public decimal? ValueAvg { get; init; }

    public decimal? ValueMin { get; init; }

    public decimal? ValueMax { get; init; }

    public decimal? ValueSum { get; init; }

    public required int ReadingCount { get; init; }
}
