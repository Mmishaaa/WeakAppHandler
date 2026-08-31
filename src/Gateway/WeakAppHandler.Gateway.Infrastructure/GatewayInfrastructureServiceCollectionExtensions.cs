using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Gateway.Application.Readings;
using WeakAppHandler.Gateway.Infrastructure.Persistence;

namespace WeakAppHandler.Gateway.Infrastructure;

public static class GatewayInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Gateway")
            ?? throw new InvalidOperationException(
                "Missing required connection string 'ConnectionStrings:Gateway'.");

        services.AddDbContext<GatewayReadDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<GatewayReadDbContext>(tags: ["ready"]);

        services.AddScoped<IGatewayReadContext, GatewayReadContext>();

        return services;
    }
}
