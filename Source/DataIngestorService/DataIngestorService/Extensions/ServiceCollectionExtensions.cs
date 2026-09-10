using DataIngestorService.Clients;
using DataIngestorService.Configuration;
using DataIngestorService.Workers;
using MassTransit;
using Microsoft.Extensions.Options;
using Shared.Extensions;

namespace DataIngestorService.Extensions;

static class ServiceCollectionExtensions
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ResiliencePipelineName = "weakapp";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            services.AddOpenApi();
            services.AddGlobalExceptionHandling();

            AddWeakAppClient(services, configuration);
            AddMessaging(services, configuration);

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
        .AddResilienceHandler(ResiliencePipelineName, static (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<WeakAppOptions>>().Value;
            WeakAppResiliencePipeline.Configure(builder, options);
        });
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMassTransit(bus =>
        {
            bus.UsingRabbitMq((context, rabbit) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                rabbit.Host(options.Host, (ushort)options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.Username);
                    host.Password(options.Password);
                });

                rabbit.ConfigureEndpoints(context);
            });
        });
    }
}
