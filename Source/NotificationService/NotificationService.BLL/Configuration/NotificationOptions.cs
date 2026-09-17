namespace NotificationService.BLL.Configuration;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public List<MetricThresholdOptions> Thresholds { get; set; } = [];
}
