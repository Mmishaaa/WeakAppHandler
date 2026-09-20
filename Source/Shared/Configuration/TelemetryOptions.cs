namespace Shared.Configuration;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public Uri? OtlpEndpoint { get; set; }
}
