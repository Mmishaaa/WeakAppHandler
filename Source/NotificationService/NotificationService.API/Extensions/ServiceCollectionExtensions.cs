using System.Text.Json.Serialization;
using MassTransit;
using NotificationService.API.Configuration;
using NotificationService.API.Consumers;
using NotificationService.BLL.Extensions;
using Shared.Extensions;
using Shared.Logging;
using Shared.Messaging;

namespace NotificationService.API.Extensions;

static class ServiceCollectionExtensions
{
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            services.AddSerilogLogging(configuration, "NotificationService");
            services.AddGlobalExceptionHandling();
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
