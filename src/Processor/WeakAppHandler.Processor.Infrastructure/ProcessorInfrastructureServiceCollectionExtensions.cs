using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Application.Stats;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
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

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ProcessingStatsState>();

        // Scoped, like the DbContext they share: a consumer resolves one recorder per delivery, and
        // the transaction it opens lives and dies inside that delivery's scope.
        services.TryAddScoped<IReadingBatchWriter, MeterReadingBatchWriter>();
        services.TryAddScoped<IngestionRecorder>();

        return services;
    }
}
