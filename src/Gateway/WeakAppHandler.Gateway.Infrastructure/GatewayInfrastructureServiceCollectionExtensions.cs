using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Gateway.Application.Alerting;
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

        // Same physical database as GatewayReadDbContext (one "gateway" role, granted SELECT on
        // every table in the public schema) - a second DbContext because the tables belong to a
        // different owning service (Notification), not because the connection differs.
        services.AddDbContext<GatewayAlertingReadDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<GatewayAlertingReadDbContext>(tags: ["ready"]);

        services.AddScoped<IGatewayAlertingReadContext, GatewayAlertingReadContext>();

        return services;
    }
}
