using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Handlers;
using Shared.UnitOfWork;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUnitOfWork<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();

        return services;
    }

    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
