using Microsoft.EntityFrameworkCore;

namespace WeakAppHandler.Notification.Api.Persistence;

public static class NotificationPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Notification")
            ?? throw new InvalidOperationException(
                "Missing required connection string 'ConnectionStrings:Notification'.");

        // See AuthPersistenceServiceCollectionExtensions for why this needs a name distinct from
        // EF Core's shared "__EFMigrationsHistory" default: Auth/Processor/Notification share one
        // physical database, and their writer roles otherwise collide over ownership of that one
        // table.
        services.AddDbContext<AlertingDbContext>(options => options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(AlertingDbContext.MigrationsHistoryTableName))
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AlertingDbContext>(tags: ["ready"]);

        return services;
    }
}
