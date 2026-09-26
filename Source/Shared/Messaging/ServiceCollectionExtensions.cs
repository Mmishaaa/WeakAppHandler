using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Shared.Messaging;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRabbitMqMessaging(
            IConfiguration configuration,
            Action<IBusRegistrationConfigurator>? configureBus = null,
            Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureTransport = null)
        {
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddMassTransit(bus =>
            {
                bus.SetKebabCaseEndpointNameFormatter();

                configureBus?.Invoke(bus);

                bus.UsingRabbitMq((context, rabbit) =>
                {
                    var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                    rabbit.Host(
                        options.Host,
                        (ushort)options.Port,
                        options.VirtualHost,
                        host =>
                        {
                            host.Username(options.Username);
                            host.Password(options.Password);
                        });

                    configureTransport?.Invoke(context, rabbit);

                    rabbit.ConfigureEndpoints(context);
                });
            });

            return services;
        }
    }
}
