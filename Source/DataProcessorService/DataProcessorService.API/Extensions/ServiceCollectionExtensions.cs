using DataProcessorService.API.Consumers;
using DataProcessorService.BLL.Extensions;
using DataProcessorService.DAL;
using MassTransit;
using Shared.Extensions;
using Shared.Messaging;

namespace DataProcessorService.API.Extensions;

static class ServiceCollectionExtensions
{
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            services.AddOpenApi();
            services.AddGlobalExceptionHandling();
            services.AddBll(configuration);

            services.AddRabbitMqMessaging(configuration, bus =>
            {
                bus.AddConsumer<MeterReadingsCapturedConsumer>();

                bus.AddEntityFrameworkOutbox<ProcessorDbContext>(outbox => outbox.UsePostgres());

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
