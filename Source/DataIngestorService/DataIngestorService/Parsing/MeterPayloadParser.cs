using System.Collections.Frozen;
using System.Text.Json;
using DataIngestorService.Contracts;
using DataIngestorService.Models;

namespace DataIngestorService.Parsing;

static class MeterPayloadParser
{
    private static readonly FrozenDictionary<string, string> FieldToMetricCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["co2"] = "co2",
            ["pm25"] = "pm25",
            ["humidity"] = "humidity",
            ["energy"] = "energy",
            ["motionDetected"] = "motion_detected",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyList<MeterReadingModel> ParseAll(IReadOnlyList<WeakAppMeterDto> meters)
    {
        ArgumentNullException.ThrowIfNull(meters);

        var readings = new List<MeterReadingModel>(meters.Count);

        foreach (var meter in meters)
        {
            AddReadings(meter, readings);
        }

        return readings;
    }

    private static void AddReadings(WeakAppMeterDto meter, List<MeterReadingModel> readings)
    {
        ArgumentNullException.ThrowIfNull(meter);

        if (meter.Payload.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var field in meter.Payload.EnumerateObject())
        {
            if (!FieldToMetricCode.TryGetValue(field.Name, out var metricCode))
            {
                continue;
            }

            switch (field.Value.ValueKind)
            {
                case JsonValueKind.Number when field.Value.TryGetDecimal(out var numeric):
                    readings.Add(new MeterReadingModel(
                        meter.Name, meter.Type, metricCode, numeric, Flag: null));
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    readings.Add(new MeterReadingModel(
                        meter.Name, meter.Type, metricCode, Numeric: null, field.Value.GetBoolean()));
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.Undefined:
                case JsonValueKind.Object:
                case JsonValueKind.Array:
                case JsonValueKind.String:
                case JsonValueKind.Null:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(meter), field.Value.ValueKind, "Unknown JSON value kind.");
            }
        }
    }
}
