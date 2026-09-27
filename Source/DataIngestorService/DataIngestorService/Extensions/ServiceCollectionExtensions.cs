using DataIngestorService.Clients;
using DataIngestorService.Configuration;
using DataIngestorService.Workers;
using Microsoft.Extensions.Options;
using Shared.Extensions;
using Shared.Logging;
using Shared.Messaging;
using Shared.Telemetry;

namespace DataIngestorService.Extensions;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            services.AddSerilogLogging(configuration, "DataIngestorService");
            services.AddTelemetry(configuration, "DataIngestorService");
            services.AddOpenApi();
            services.AddGlobalExceptionHandling();
            services.AddHealthChecks();

            AddWeakAppClient(services, configuration);

            services.AddRabbitMqMessaging(configuration);

            services.AddHostedService<MeterIngestionWorker>();

            return services;
        }
    }

    // Below the extension block because StyleCop does not recognise extension blocks yet and
    // reports any of them that follows a field or a property (SA1201).
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ResiliencePipelineName = "weakapp";

    private static void AddWeakAppClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WeakAppOptions>()
            .Bind(configuration.GetSection(WeakAppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IWeakAppApiClient, WeakAppApiClient>(static (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<WeakAppOptions>>().Value;

            client.BaseAddress = options.BaseUrl;
            client.DefaultRequestHeaders.Add(ApiKeyHeaderName, options.ApiKey);
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddResilienceHandler(
            ResiliencePipelineName,
            static (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<WeakAppOptions>>().Value;
                WeakAppResiliencePipeline.Configure(builder, options);
            });
    }
}
