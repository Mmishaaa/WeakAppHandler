using GraphQlGateway.BLL.Services;
using GraphQlGateway.DAL.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphQlGateway.BLL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBll(IConfiguration configuration)
        {
            services.AddDal(configuration);

            services.AddScoped<IReadingStatsService, ReadingStatsService>();

            return services;
        }
    }
}
