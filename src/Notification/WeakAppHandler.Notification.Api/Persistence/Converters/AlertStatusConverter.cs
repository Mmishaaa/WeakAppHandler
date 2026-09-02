using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Persistence.Converters;

/// <summary>
/// Stores <see cref="AlertStatus"/> as the lower-case codes PRD §7.1 documents (`active`,
/// `resolved`). The partial index on active alerts is written against these literals, so the
/// spelling here and the index filter have to agree.
/// </summary>
public sealed class AlertStatusConverter : ValueConverter<AlertStatus, string>
{
    public AlertStatusConverter()
        : base(status => ToCode(status), code => FromCode(code))
    {
    }

    public static string ToCode(AlertStatus value) => value switch
    {
        AlertStatus.Active => "active",
        AlertStatus.Resolved => "resolved",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown alert status."),
    };

    public static AlertStatus FromCode(string code) => code switch
    {
        "active" => AlertStatus.Active,
        "resolved" => AlertStatus.Resolved,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown alert status code."),
    };
}
