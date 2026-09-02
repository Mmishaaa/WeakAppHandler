using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeakAppHandler.Gateway.Application.Alerting;

namespace WeakAppHandler.Gateway.Infrastructure.Persistence.Converters;

/// <summary>
/// Reads <c>alert_rules.operator</c> as the lower-case codes Notification's own
/// AlertOperatorConverter writes (`gt`, `gte`, `lt`, `lte`, `eq`).
/// </summary>
public sealed class AlertOperatorConverter : ValueConverter<AlertOperator, string>
{
    public AlertOperatorConverter()
        : base(op => ToCode(op), code => FromCode(code))
    {
    }

    public static string ToCode(AlertOperator value) => value switch
    {
        AlertOperator.Gt => "gt",
        AlertOperator.Gte => "gte",
        AlertOperator.Lt => "lt",
        AlertOperator.Lte => "lte",
        AlertOperator.Eq => "eq",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown alert operator."),
    };

    public static AlertOperator FromCode(string code) => code switch
    {
        "gt" => AlertOperator.Gt,
        "gte" => AlertOperator.Gte,
        "lt" => AlertOperator.Lt,
        "lte" => AlertOperator.Lte,
        "eq" => AlertOperator.Eq,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown alert operator code."),
    };
}
