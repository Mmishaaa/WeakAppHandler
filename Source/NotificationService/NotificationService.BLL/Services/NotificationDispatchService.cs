using Microsoft.Extensions.Options;
using NotificationService.BLL.Models;
using NotificationService.BLL.Notifications;
using Shared.Configuration;

namespace NotificationService.BLL.Services;

public sealed class NotificationDispatchService(IOptionsMonitor<ThresholdOptions> options)
    : INotificationDispatchService
{
    public NotificationDispatchModel Dispatch(IReadOnlyList<ReadingNotificationModel> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var thresholds = ResolveThresholds();

        var alerts = readings
            .Select(reading => Evaluate(reading, thresholds))
            .OfType<ReadingAlertModel>()
            .ToList();

        var locations = readings
            .Select(reading => reading.Location)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var metricCodes = readings
            .Select(reading => reading.MetricCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var envelopes = new List<NotificationEnvelopeModel>
        {
            Envelope(NotificationGroups.All, readings, alerts, _ => true),
        };

        envelopes.AddRange(locations.Select(location => Envelope(
            NotificationGroups.ForLocation(location),
            readings,
            alerts,
            reading => reading.Location == location)));

        envelopes.AddRange(metricCodes.Select(metricCode => Envelope(
            NotificationGroups.ForMetric(metricCode),
            readings,
            alerts,
            reading => reading.MetricCode == metricCode)));

        envelopes.AddRange(
            from location in locations
            from metricCode in metricCodes
            select Envelope(
                NotificationGroups.ForLocationAndMetric(location, metricCode),
                readings,
                alerts,
                reading => reading.Location == location && reading.MetricCode == metricCode));

        return new NotificationDispatchModel(
            [.. envelopes.Where(envelope => envelope.Readings.Count > 0)],
            readings.Count,
            alerts.Count);
    }

    private Dictionary<string, MetricThresholdOptions> ResolveThresholds()
    {
        var thresholds = new Dictionary<string, MetricThresholdOptions>(StringComparer.OrdinalIgnoreCase);

        foreach (var threshold in options.CurrentValue.Metrics)
        {
            thresholds[threshold.MetricCode] = threshold;
        }

        return thresholds;
    }

    private static NotificationEnvelopeModel Envelope(
        string group,
        IReadOnlyList<ReadingNotificationModel> readings,
        IReadOnlyList<ReadingAlertModel> alerts,
        Func<ReadingNotificationModel, bool> matches) =>
        new(
            group,
            [.. readings.Where(matches)],
            [.. alerts.Where(alert => matches(alert.Reading))]);

    private static ReadingAlertModel? Evaluate(
        ReadingNotificationModel reading,
        IReadOnlyDictionary<string, MetricThresholdOptions> thresholds)
    {
        if (reading.Numeric is not { } value)
        {
            return null;
        }

        if (!thresholds.TryGetValue(reading.MetricCode, out var threshold))
        {
            return null;
        }

        if (threshold.Min is { } min && value < min)
        {
            return new ReadingAlertModel(reading, AlertKind.Below, min);
        }

        if (threshold.Max is { } max && value > max)
        {
            return new ReadingAlertModel(reading, AlertKind.Above, max);
        }

        return null;
    }
}
