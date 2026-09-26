using System.Diagnostics;

namespace Shared.Telemetry;

public static class IngestionTracing
{
    public const string SourceName = "WeakAppHandler";

    public static ActivitySource Source { get; } = new(SourceName);
}
