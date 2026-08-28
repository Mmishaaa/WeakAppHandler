using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Polling;

/// <summary>
/// Turns WeakApp's wire representation into the transport envelope (PRD §6 F2). The payload stays
/// opaque JSON text: flattening it into per-metric rows is the Processor's job (F3), and the
/// Ingestor deliberately knows nothing about which metric codes a meter type carries.
/// </summary>
internal static class MeterReadingEnvelopeMapper
{
    /// <summary>
    /// Stand-in payload for a meter element that arrived without a <c>payload</c> property at all.
    /// <see cref="JsonElement.GetRawText"/> throws on an undefined element, which would fail the
    /// whole batch over one malformed entry; an empty object normalises to zero metric rows
    /// downstream instead, which is what a payload-less meter genuinely contributes.
    /// </summary>
    private const string MissingPayload = "{}";

    public static IReadOnlyList<MeterReadingEnvelope> Map(IReadOnlyList<WeakAppMeterDto> meters)
    {
        var envelopes = new List<MeterReadingEnvelope>(meters.Count);

        foreach (var meter in meters)
        {
            var payload = meter.Payload.ValueKind == JsonValueKind.Undefined
                ? MissingPayload
                : meter.Payload.GetRawText();

            // WeakApp's `name` is the room the meter sits in; `type` is the sensor kind. Together
            // they are the natural identity the Processor registers meters under (PRD §6 F3).
            envelopes.Add(new MeterReadingEnvelope(meter.Name, meter.Type, payload, ComputeHash(payload)));
        }

        return envelopes;
    }

    private static string ComputeHash(string payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
}
