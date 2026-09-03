using System.Diagnostics;

namespace WeakAppHandler.Ingestor.Telemetry;

/// <summary>
/// The Ingestor's own tracing source (TASK-044). <see cref="Polling.IngestionPoller"/> starts a span
/// on it spanning the whole poll attempt - the HTTP call to WeakApp and both publishes - because the built-in
/// HTTP client span it wraps ends the moment the response is read, and without an ambient parent still
/// live at publish time, MassTransit's own span would start a brand new trace instead of continuing
/// this one. Registered with the OTel SDK via the "WeakAppHandler.*" wildcard in ServiceDefaults.
/// </summary>
internal static class IngestorActivitySource
{
    public const string Name = "WeakAppHandler.Ingestor";

    public static readonly ActivitySource Instance = new(Name);
}
