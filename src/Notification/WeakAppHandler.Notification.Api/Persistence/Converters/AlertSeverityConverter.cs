using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Persistence.Converters;

/// <summary>
/// Stores <see cref="AlertSeverity"/> as the lower-case codes PRD §7.1 documents (`info`, `warning`,
/// `critical`).
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
