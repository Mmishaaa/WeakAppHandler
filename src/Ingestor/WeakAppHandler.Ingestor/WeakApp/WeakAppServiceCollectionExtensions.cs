using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;

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

        // Singleton because the pipeline it gets attached to is built once and lives as long as the
        // handler; the admin API resolves the same instance to report the breaker's current state.
        builder.Services.TryAddSingleton<CircuitBreakerStateProvider>();

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
                var circuitBreakerState = context.ServiceProvider.GetRequiredService<CircuitBreakerStateProvider>();
                WeakAppResiliencePipelineFactory.Configure(
                    pipelineBuilder, options, timeProvider, circuitBreakerState);
            });
    }
}
