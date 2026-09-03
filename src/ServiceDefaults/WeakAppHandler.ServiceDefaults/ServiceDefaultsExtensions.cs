using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.ServiceDefaults;

/// <summary>
/// Cross-cutting wiring (logging, tracing/metrics, health checks, auth) shared by every service
/// host. Call <see cref="AddServiceDefaults"/> during host setup and
/// <see cref="MapServiceDefaultsEndpoints"/> once the app is built.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureServiceLogging();
        builder.ConfigureServiceTelemetry();
        builder.ConfigureServiceHealthChecks();
        builder.AddServiceAuthentication();

        return builder;
    }

    public static WebApplication MapServiceDefaultsEndpoints(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        app.MapPrometheusScrapingEndpoint("/metrics");

        return app;
    }

    private static void ConfigureServiceLogging(this IHostApplicationBuilder builder)
    {
        var serviceName = builder.Environment.ApplicationName;

        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(new CompactJsonFormatter());
        });
    }

    private static void ConfigureServiceTelemetry(this IHostApplicationBuilder builder)
    {
        var serviceName = builder.Environment.ApplicationName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()

                    // Every service's own domain meter is named "WeakAppHandler.<Service>" (e.g.
                    // IngestorMetrics, ProcessorMetrics) - the wildcard picks all of them up without
                    // ServiceDefaults having to know each service's meter name individually.
                    .AddMeter("WeakAppHandler.*")

                    // MassTransit's own built-in Meter (enabled by ServiceMassTransitExtensions'
                    // UseInstrumentation() call), reporting messaging.masstransit.receive/consume
                    // tagged by destination - TASK-044's "queue consumption rate" F10 metric.
                    .AddMeter("MassTransit")
                    .AddPrometheusExporter();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()

                    // MassTransit only injects/extracts W3C traceparent into message headers - and
                    // only creates spans for Send/Publish/Consume - once a listener is registered for
                    // its own "MassTransit" ActivitySource; without this, ActivitySource.StartActivity
                    // is a no-op and trace context never crosses RabbitMQ (TASK-044).
                    .AddSource("MassTransit")

                    // Same wildcard convention as the meter above, for each service's own
                    // ActivitySource (e.g. the Ingestor's poll-cycle span that HTTP/publish spans nest
                    // under so a single trace id survives the hop from WeakApp into the bus).
                    .AddSource("WeakAppHandler.*");

                var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                }
            });
    }

    private static void ConfigureServiceHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
    }
}
