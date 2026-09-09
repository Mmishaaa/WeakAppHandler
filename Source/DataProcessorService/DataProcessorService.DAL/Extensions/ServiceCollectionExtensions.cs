using DataProcessorService.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Extensions;

namespace DataProcessorService.DAL.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProcessorDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddUnitOfWork<ProcessorDbContext>();

        services.AddScoped<IMeterRepository, MeterRepository>();
        services.AddScoped<IReadingRepository, ReadingRepository>();
        services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();

        return services;
    }
}
