using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataIngestorService.Contracts;

sealed record WeakAppMeterDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("payload")] JsonElement Payload);
