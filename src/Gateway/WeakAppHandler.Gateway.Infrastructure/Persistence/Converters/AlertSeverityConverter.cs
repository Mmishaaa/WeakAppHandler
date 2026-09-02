using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeakAppHandler.Gateway.Application.Alerting;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Converters;

/// <summary>
/// Reads <c>alert_rules.severity</c>/<c>alerts.severity</c> as the lower-case codes Notification's
/// own AlertSeverityConverter writes (`info`, `warning`, `critical`).
/// </summary>
public sealed class AlertSeverityConverter : ValueConverter<AlertSeverity, string>
{
    public AlertSeverityConverter()
        : base(severity => ToCode(severity), code => FromCode(code))
    {
    }

    public static string ToCode(AlertSeverity value) => value switch
    {
        AlertSeverity.Info => "info",
        AlertSeverity.Warning => "warning",
        AlertSeverity.Critical => "critical",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown alert severity."),
    };

    public static AlertSeverity FromCode(string code) => code switch
    {
        "info" => AlertSeverity.Info,
        "warning" => AlertSeverity.Warning,
        "critical" => AlertSeverity.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown alert severity code."),
    };
}
