using DataIngestorService.Clients;
using DataIngestorService.Configuration;
using Microsoft.Extensions.Options;

namespace DataIngestorService.Extensions;

static class ServiceCollectionExtensions
{
    private const string ApiKeyHeaderName = "X-Api-Key";

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
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Add(ApiKeyHeaderName, options.ApiKey);
        });

        return services;
    }
}
