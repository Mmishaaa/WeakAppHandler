using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.BLL.Configuration;
using NotificationService.BLL.Services;

namespace NotificationService.BLL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBll(IConfiguration configuration)
        {
            services.Configure<NotificationOptions>(
                configuration.GetSection(NotificationOptions.SectionName));

            services.AddSingleton<INotificationDispatchService, NotificationDispatchService>();

            return services;
        }
    }
}
