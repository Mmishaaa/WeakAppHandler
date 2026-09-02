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

        services.AddDbContext<AlertingDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AlertingDbContext>(tags: ["ready"]);

        return services;
    }
}
