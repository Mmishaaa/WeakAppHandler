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
                    .AddPrometheusExporter();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

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
