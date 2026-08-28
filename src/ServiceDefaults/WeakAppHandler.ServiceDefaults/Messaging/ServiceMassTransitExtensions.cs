using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.ServiceDefaults.Messaging;

/// <summary>
/// Base MassTransit/RabbitMQ wiring shared by every service host. The ingestion exchange, routing
/// keys and durable queues live in <see cref="ReadingsTopologyExtensions"/> and are applied to
/// every bus built here, so publisher and consumer cannot disagree about the topology.
/// </summary>
public static class ServiceMassTransitExtensions
{
    private const ushort DefaultAmqpPort = 5672;

    public static IHostApplicationBuilder AddServiceMassTransit(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureReceiveEndpoints = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var rabbitMqSection = builder.Configuration.GetSection("RabbitMq");

        builder.Services.AddMassTransit(busConfigurator =>
        {
            configureConsumers?.Invoke(busConfigurator);

            busConfigurator.UsingRabbitMq((context, rabbitMqConfigurator) =>
            {
                // The port is explicit rather than left to the AMQP default so a host can reach a
                // broker published on a non-default port, which is the normal case for the
                // Testcontainers-backed integration tests.
                var port = ushort.TryParse(rabbitMqSection["Port"], out var configuredPort)
                    ? configuredPort
                    : DefaultAmqpPort;

                rabbitMqConfigurator.Host(
                    rabbitMqSection["Host"] ?? "localhost",
                    port,
                    rabbitMqSection["VirtualHost"] ?? "/",
                    hostConfigurator =>
                    {
                        hostConfigurator.Username(rabbitMqSection["Username"] ?? "guest");
                        hostConfigurator.Password(rabbitMqSection["Password"] ?? "guest");
                    });

                rabbitMqConfigurator.ConfigureReadingsTopology();

                configureReceiveEndpoints?.Invoke(context, rabbitMqConfigurator);

                // Consumers already placed on an explicit receive endpoint above are skipped here,
                // so a host can mix hand-configured ingestion endpoints with convention-named ones.
                rabbitMqConfigurator.ConfigureEndpoints(context);
            });
        });

        return builder;
    }
}
