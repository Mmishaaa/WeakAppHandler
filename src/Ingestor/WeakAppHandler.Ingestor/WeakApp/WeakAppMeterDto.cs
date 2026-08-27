using System.Text.Json;

namespace WeakAppHandler.Ingestor.WeakApp;

/// <summary>
/// One element of WeakApp's <c>GET /meters</c> array, exactly as observed on the wire
/// (camelCase, no envelope, no timestamp/id) - see docs/weakapp-observed-response.json.
/// Payload shape depends on <see cref="Type"/> (energy/air_quality/motion) and is left raw;
/// normalising it into flat metric rows is the Processor's job (F3), not the Ingestor's.
/// </summary>
public sealed record WeakAppMeterDto(string Type, string Name, JsonElement Payload);
