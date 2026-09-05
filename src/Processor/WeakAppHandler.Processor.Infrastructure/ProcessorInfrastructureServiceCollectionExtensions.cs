using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeakAppHandler.Processor.Application.Ingestion;
using WeakAppHandler.Processor.Application.Stats;
using WeakAppHandler.Processor.Application.Telemetry;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.Processor.Infrastructure.Persistence;
using WeakAppHandler.Processor.Infrastructure.Retention;

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

        // See AuthPersistenceServiceCollectionExtensions for why this needs a name distinct from
        // EF Core's shared "__EFMigrationsHistory" default: Auth/Processor/Notification share one
        // physical database, and their writer roles otherwise collide over ownership of that one
        // table.
        services.AddDbContext<CoreDbContext>(options => options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTableName))
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<CoreDbContext>(tags: ["ready"]);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ProcessingStatsState>();
        services.TryAddSingleton<ProcessorMetrics>();

        // Scoped, like the DbContext they share: a consumer resolves one recorder per delivery, and
        // the transaction it opens lives and dies inside that delivery's scope.
        services.TryAddScoped<IReadingBatchWriter, MeterReadingBatchWriter>();
        services.TryAddScoped<IngestionRecorder>();

        services.AddOptions<RetentionOptions>().Bind(configuration.GetSection(RetentionOptions.SectionName));
        services.TryAddScoped<IRetentionJob, RetentionJob>();

        return services;
    }
}
