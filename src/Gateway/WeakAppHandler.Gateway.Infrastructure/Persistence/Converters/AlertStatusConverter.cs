using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeakAppHandler.Gateway.Application.Alerting;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Converters;

/// <summary>
/// Reads <c>alerts.status</c> as the lower-case codes Notification's own
/// AlertStatusConverter writes (`active`, `resolved`) - the two spellings must not diverge, since
/// this is the same column, not a copy of it.
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
