using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Handlers;
using Shared.UnitOfWork;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUnitOfWork<TDbContext>()
            where TDbContext : DbContext
        {
            services.AddScoped<DbContext>(provider => provider.GetRequiredService<TDbContext>());
            services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();

            return services;
        }

        public IServiceCollection AddGlobalExceptionHandling()
        {
            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();

            return services;
        }
    }
}
