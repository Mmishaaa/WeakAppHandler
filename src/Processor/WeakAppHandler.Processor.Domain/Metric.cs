namespace WeakAppHandler.Processor.Domain;

public sealed class Metric
{
    public required string Code { get; init; }

    public required string MeterType { get; set; }

    public required string Unit { get; set; }

    public required MetricValueKind ValueKind { get; set; }

    public required string DisplayName { get; set; }
}
