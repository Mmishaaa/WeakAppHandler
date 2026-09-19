using GraphQlGateway.BLL.Services;
using GraphQlGateway.DAL.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace GraphQlGateway.BLL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBll(IConfiguration configuration)
        {
            services.AddDal(configuration);

            services.Configure<ThresholdOptions>(
                configuration.GetSection(ThresholdOptions.SectionName));

            services.AddScoped<IReadingStatsService, ReadingStatsService>();
            services.AddScoped<IFilterOptionsService, FilterOptionsService>();

            return services;
        }
    }
}
