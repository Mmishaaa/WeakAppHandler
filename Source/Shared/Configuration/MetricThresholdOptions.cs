namespace Shared.Configuration;

public sealed class MetricThresholdOptions
{
    public string MetricCode { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }
}
