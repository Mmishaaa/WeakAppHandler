using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.Gateway.Api.ServiceClients;

/// <summary>
/// Wires everything TASK-026's admin proxy needs to call the Ingestor's/Processor's admin APIs as a
/// machine client: the client-credentials token provider, and one named <see cref="HttpClient"/> per
/// downstream service pointed at its configured base address.
/// </summary>
public static class ServiceClientsServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddDownstreamServiceClients(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<ServiceClientOptions>()
            .Bind(builder.Configuration.GetSection(ServiceClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHttpClient(ServiceClientTokenProvider.HttpClientName);
        builder.Services.AddSingleton<ServiceClientTokenProvider>();

        var servicesSection = builder.Configuration.GetSection("Services");

        builder.Services.AddHttpClient(DownstreamServiceNames.Ingestor, client =>
            client.BaseAddress = new Uri(RequireBaseUrl(servicesSection, "Ingestor")));
        builder.Services.AddHttpClient(DownstreamServiceNames.Processor, client =>
            client.BaseAddress = new Uri(RequireBaseUrl(servicesSection, "Processor")));

        return builder;
    }

    private static string RequireBaseUrl(IConfigurationSection servicesSection, string serviceName) =>
        servicesSection[$"{serviceName}:BaseUrl"]
            ?? throw new InvalidOperationException($"Missing required configuration 'Services:{serviceName}:BaseUrl'.");
}
