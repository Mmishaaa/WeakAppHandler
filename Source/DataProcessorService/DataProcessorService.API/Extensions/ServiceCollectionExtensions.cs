using DataProcessorService.API.Consumers;
using DataProcessorService.BLL.Extensions;
using DataProcessorService.DAL;
using MassTransit;
using Shared.Extensions;
using Shared.HealthChecks;
using Shared.Logging;
using Shared.Messaging;
using Shared.Telemetry;

namespace DataProcessorService.API.Extensions;

internal static class ServiceCollectionExtensions
{
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan DuplicateDetectionWindow = TimeSpan.FromDays(1);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            services.AddSerilogLogging(configuration, "DataProcessorService");
            services.AddTelemetry(configuration, "DataProcessorService");
            services.AddOpenApi();
            services.AddGlobalExceptionHandling();
            services.AddBll(configuration);
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck<ProcessorDbContext>>("database");

            services.AddRabbitMqMessaging(
                configuration,
                bus =>
                {
                    bus.AddConsumer<MeterReadingsCapturedConsumer>();

                    bus.AddEntityFrameworkOutbox<ProcessorDbContext>(outbox =>
                    {
                        outbox.UsePostgres();
                        outbox.DuplicateDetectionWindow = DuplicateDetectionWindow;
                    });

                    bus.AddConfigureEndpointsCallback((context, queueName, endpoint) =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Interval(RetryCount, RetryInterval));
                        endpoint.UseEntityFrameworkOutbox<ProcessorDbContext>(context);
                    });
                });

            return services;
        }
    }
}
