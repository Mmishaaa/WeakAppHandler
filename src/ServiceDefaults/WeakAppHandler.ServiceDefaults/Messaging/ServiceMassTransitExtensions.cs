using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace WeakAppHandler.ServiceDefaults.Messaging;

/// <summary>
/// Base MassTransit/RabbitMQ wiring shared by every service host. Exchange/queue topology
/// (durable queues, dead-lettering) is configured per-message-type on top of this in TASK-012.
/// </summary>
public static class ServiceMassTransitExtensions
{
    public static IHostApplicationBuilder AddServiceMassTransit(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var rabbitMqSection = builder.Configuration.GetSection("RabbitMq");

        builder.Services.AddMassTransit(busConfigurator =>
        {
            configureConsumers?.Invoke(busConfigurator);

            busConfigurator.UsingRabbitMq((context, rabbitMqConfigurator) =>
            {
                rabbitMqConfigurator.Host(
                    rabbitMqSection["Host"] ?? "localhost",
                    rabbitMqSection["VirtualHost"] ?? "/",
                    hostConfigurator =>
                    {
                        hostConfigurator.Username(rabbitMqSection["Username"] ?? "guest");
                        hostConfigurator.Password(rabbitMqSection["Password"] ?? "guest");
                    });

                rabbitMqConfigurator.ConfigureEndpoints(context);
            });
        });

        return builder;
    }
}
