using DataProcessorService.BLL.Services;
using DataProcessorService.DAL.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataProcessorService.BLL.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBll(IConfiguration configuration)
        {
            services.AddDal(configuration);

            services.AddScoped<IReadingBatchService, ReadingBatchService>();

            return services;
        }
    }
}
