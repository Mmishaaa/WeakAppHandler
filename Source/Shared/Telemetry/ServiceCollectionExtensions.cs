using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Configuration;

namespace Shared.Telemetry;

public static class ServiceCollectionExtensions
{
    private const string MassTransitSourceName = "MassTransit";
    private const string NpgsqlSourceName = "Npgsql";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddTelemetry(IConfiguration configuration, string application)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddMetrics();
            services.AddSingleton<IngestionMetrics>();

            var options = configuration
                .GetSection(TelemetryOptions.SectionName)
                .Get<TelemetryOptions>() ?? new TelemetryOptions();

            var builder = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(application))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(IngestionMetrics.MeterName)
                    .AddMeter(MassTransitSourceName)
                    .AddMeter(NpgsqlSourceName)
                    .AddPrometheusExporter());

            if (options.OtlpEndpoint is not { } endpoint)
            {
                return services;
            }

            builder.WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(MassTransitSourceName)
                .AddSource(NpgsqlSourceName)
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint));

            return services;
        }
    }
}
