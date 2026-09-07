using DataProcessorService.Configuration;
using DataProcessorService.Consumers;
using MassTransit;
using Microsoft.Extensions.Options;

namespace DataProcessorService.Extensions;

static class ServiceCollectionExtensions
{
    private const int RetryCount = 3;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        return services;
    }
}
