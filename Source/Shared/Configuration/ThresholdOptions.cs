namespace Shared.Configuration;

public sealed class ThresholdOptions
{
    public const string SectionName = "Thresholds";

    public List<MetricThresholdOptions> Metrics { get; set; } = [];
}
