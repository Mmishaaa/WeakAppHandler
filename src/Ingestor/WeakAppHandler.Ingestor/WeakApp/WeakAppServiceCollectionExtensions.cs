using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace WeakAppHandler.Ingestor.WeakApp;

public static class WeakAppServiceCollectionExtensions
{
    public static Microsoft.Extensions.Http.Resilience.IHttpResiliencePipelineBuilder AddWeakAppClient(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<WeakAppOptions>()
            .Bind(builder.Configuration.GetSection(WeakAppOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WeakAppOptions>, WeakAppOptionsValidator>();
        builder.Services.TryAddSingleton(TimeProvider.System);

        return builder.Services
            .AddHttpClient<IWeakAppClient, WeakAppClient>((services, client) =>
            {
                var options = services.GetRequiredService<IOptions<WeakAppOptions>>().Value;
                client.BaseAddress = options.BaseUrl;
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            })
            .AddResilienceHandler("weakapp-pipeline", (pipelineBuilder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<WeakAppOptions>>().Value;
                var timeProvider = context.ServiceProvider.GetRequiredService<TimeProvider>();
                WeakAppResiliencePipelineFactory.Configure(pipelineBuilder, options, timeProvider);
            });
    }
}
