using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeakAppHandler.Notification.Api.Domain;

namespace WeakAppHandler.Notification.Api.Persistence.Converters;

/// <summary>
/// Stores <see cref="AlertOperator"/> as the lower-case codes PRD §7.1 documents (`gt`, `gte`, `lt`,
/// `lte`, `eq`) rather than the CLR names EnumToStringConverter would produce. The column is part of
/// the REST contract in TASK-030 and of what an operator reads in psql, so the two spellings must not
/// diverge.
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
