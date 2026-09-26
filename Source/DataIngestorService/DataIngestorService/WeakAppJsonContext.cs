using System.Text.Json;
using System.Text.Json.Serialization;
using DataIngestorService.Contracts;

namespace DataIngestorService;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(List<WeakAppMeterDto>))]
internal sealed partial class WeakAppJsonContext : JsonSerializerContext;
