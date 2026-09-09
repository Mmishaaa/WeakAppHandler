using DataProcessorService.BLL.Services;
using DataProcessorService.DAL.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataProcessorService.BLL.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBll(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDal(configuration);

        services.AddScoped<IReadingBatchService, ReadingBatchService>();

        return services;
    }
}
