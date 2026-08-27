using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure;

public static class ProcessorInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddProcessorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Processor")
            ?? throw new InvalidOperationException(
                "Missing required connection string 'ConnectionStrings:Processor'.");

        services.AddDbContext<CoreDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<CoreDbContext>(tags: ["ready"]);

        return services;
    }
}
