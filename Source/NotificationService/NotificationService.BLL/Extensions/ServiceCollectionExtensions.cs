using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.BLL.Services;
using Shared.Configuration;

namespace NotificationService.BLL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBll(IConfiguration configuration)
        {
            services.Configure<ThresholdOptions>(
                configuration.GetSection(ThresholdOptions.SectionName));

            services.AddSingleton<INotificationDispatchService, NotificationDispatchService>();

            return services;
        }
    }
}
