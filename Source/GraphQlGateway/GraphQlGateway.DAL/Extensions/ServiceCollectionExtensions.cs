using GraphQlGateway.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace GraphQlGateway.DAL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDal(IConfiguration configuration)
        {
            services.AddDbContextFactory<GatewayDbContext>(options =>
                options
                    .UseNpgsql(configuration.GetDatabaseConnectionString())
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddScoped<IReadingRepository, ReadingRepository>();
            services.AddScoped<IFilterOptionsRepository, FilterOptionsRepository>();

            return services;
        }
    }
}
