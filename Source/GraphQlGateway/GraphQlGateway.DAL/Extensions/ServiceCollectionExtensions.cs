using GraphQlGateway.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphQlGateway.DAL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDal(IConfiguration configuration)
        {
            services.AddDbContextFactory<GatewayDbContext>(options =>
                options
                    .UseNpgsql(configuration.GetConnectionString("Database"))
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddScoped<IReadingRepository, ReadingRepository>();

            return services;
        }
    }
}
