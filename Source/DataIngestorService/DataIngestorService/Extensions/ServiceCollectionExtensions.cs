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
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ResiliencePipelineName = "weakapp";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            services.AddSerilogLogging(configuration, "DataIngestorService");
            services.AddTelemetry(configuration, "DataIngestorService");
            services.AddOpenApi();
            services.AddGlobalExceptionHandling();

            AddWeakAppClient(services, configuration);

            services.AddRabbitMqMessaging(configuration);

            services.AddHostedService<MeterIngestionWorker>();

            return services;
        }
    }

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
