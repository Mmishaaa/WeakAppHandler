using DataProcessorService.API.Configuration;
using DataProcessorService.API.Consumers;
using DataProcessorService.BLL.Extensions;
using MassTransit;
using Microsoft.Extensions.Options;
using Shared.Extensions;

namespace DataProcessorService.API.Extensions;

static class ServiceCollectionExtensions
{
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddGlobalExceptionHandling();
        services.AddBll(configuration);

        AddMessaging(services, configuration);

        return services;
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<MeterReadingsCapturedConsumer>();
            bus.SetKebabCaseEndpointNameFormatter();
            bus.UsingRabbitMq((context, rabbit) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                rabbit.Host(options.Host, (ushort)options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.Username);
                    host.Password(options.Password);
                });

                rabbit.UseMessageRetry(retry => retry.Interval(RetryCount, RetryInterval));

                rabbit.ConfigureEndpoints(context);
            });
        });
    }
}
