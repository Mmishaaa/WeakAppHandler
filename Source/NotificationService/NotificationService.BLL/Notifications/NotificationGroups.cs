namespace NotificationService.BLL.Notifications;

public static class NotificationGroups
{
    public const string All = "readings:all";

    public static string ForLocation(string location) => $"readings:location:{location}";

    public static string ForMetric(string metricCode) => $"readings:metric:{metricCode}";

    public static string ForLocationAndMetric(string location, string metricCode) =>
        $"readings:location:{location}:metric:{metricCode}";

    public static string For(string? location, string? metricCode) =>
        (location, metricCode) switch
        {
            ({ Length: > 0 } scopedLocation, { Length: > 0 } scopedMetric) =>
                ForLocationAndMetric(scopedLocation, scopedMetric),
            ({ Length: > 0 } scopedLocation, _) => ForLocation(scopedLocation),
            (_, { Length: > 0 } scopedMetric) => ForMetric(scopedMetric),
            _ => All,
        };
}
