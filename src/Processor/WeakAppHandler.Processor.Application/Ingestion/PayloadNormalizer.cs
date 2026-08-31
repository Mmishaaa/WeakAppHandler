using System.Text.Json;

namespace WeakAppHandler.Processor.Application.Ingestion;

/// <summary>
/// Flattens one meter's opaque WeakApp payload into <see cref="NormalizedMetricValue"/> rows (PRD §6
/// F3). Pure and IO-free on purpose: which field became which metric code, and whether a value
/// parsed as numeric or boolean, must be provable without a database or a message bus.
/// </summary>
public static class PayloadNormalizer
{
    // WeakApp's wire field name -> readings.metric_code. One flat table rather than one per meter
    // type: none of the observed field names (energy, co2, pm25, humidity, motionDetected) collide
    // across types, and metrics.meter_type already scopes each code (docs/weakapp-observed-response.json).
    private static readonly Dictionary<string, string> FieldToMetricCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["energy"] = "energy",
            ["co2"] = "co2",
            ["pm25"] = "pm25",
            ["humidity"] = "humidity",
            ["motionDetected"] = "motion_detected",
        };

    /// <summary>
    /// Parses a payload object and returns one value per recognised field. A field with no entry in
    /// <see cref="FieldToMetricCode"/> is skipped rather than failing the whole meter: an unmapped
    /// field showing up in an otherwise well-formed payload should not turn a successful poll into a
    /// lost batch.
    /// </summary>
    public static IReadOnlyList<NormalizedMetricValue> Normalize(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);

        using var document = JsonDocument.Parse(payloadJson);
        var values = new List<NormalizedMetricValue>();

        foreach (var field in document.RootElement.EnumerateObject())
        {
            if (!FieldToMetricCode.TryGetValue(field.Name, out var metricCode))
            {
                continue;
            }

            values.Add(field.Value.ValueKind switch
            {
                JsonValueKind.True or JsonValueKind.False =>
                    new NormalizedMetricValue(metricCode, ValueNumeric: null, field.Value.GetBoolean()),
                JsonValueKind.Number =>
                    new NormalizedMetricValue(metricCode, field.Value.GetDecimal(), ValueBool: null),
                var kind => throw new FormatException(
                    $"Payload field '{field.Name}' has unsupported JSON value kind '{kind}'."),
            });
        }

        return values;
    }
}
