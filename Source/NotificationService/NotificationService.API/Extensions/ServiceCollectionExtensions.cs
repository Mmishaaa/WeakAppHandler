using System.Text.Json.Serialization;
using MassTransit;
using NotificationService.API.Configuration;
using NotificationService.API.Consumers;
using NotificationService.BLL.Extensions;
using Shared.Extensions;
using Shared.Logging;
using Shared.Messaging;
using Shared.Telemetry;

namespace NotificationService.API.Extensions;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            services.AddSerilogLogging(configuration, "NotificationService");
            services.AddTelemetry(configuration, "NotificationService");
            services.AddGlobalExceptionHandling();
            services.AddHealthChecks();
            services.AddSignalR()
                .AddJsonProtocol(json =>
                    json.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            services.AddBll(configuration);

            AddClientAppCors(services, configuration);

            services.AddRabbitMqMessaging(
                configuration,
                bus => bus.AddConsumer<MeterReadingsStoredConsumer>(),
                (context, rabbit) => rabbit.UseMessageRetry(retry => retry.Interval(RetryCount, RetryInterval)));

            return services;
        }
    }

    // Below the extension block because StyleCop does not recognise extension blocks yet and
    // reports any of them that follows a field or a property (SA1201).
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private static void AddClientAppCors(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ClientAppOptions.SectionName)
            .Get<ClientAppOptions>() ?? new ClientAppOptions();

        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .WithOrigins([.. options.AllowedOrigins])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
    }
}
