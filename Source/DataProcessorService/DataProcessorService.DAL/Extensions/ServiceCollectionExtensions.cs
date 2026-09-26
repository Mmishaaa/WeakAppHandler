using DataProcessorService.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Extensions;

namespace DataProcessorService.DAL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDal(IConfiguration configuration)
        {
            services.AddDbContext<ProcessorDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("Database")));

            services.AddUnitOfWork<ProcessorDbContext>();

            services.AddScoped<IMeterRepository, MeterRepository>();
            services.AddScoped<IReadingRepository, ReadingRepository>();

            return services;
        }
    }
}
