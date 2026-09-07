using DataIngestorService.Clients;
using DataIngestorService.Configuration;
using Microsoft.Extensions.Options;

namespace DataIngestorService.Extensions;

static class ServiceCollectionExtensions
{
    private const string ApiKeyHeaderName = "X-Api-Key"; 
    private const string ResiliencePipelineName = "weakapp";

    public static IServiceCollection AddWeakAppClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        return services;
    }
}
