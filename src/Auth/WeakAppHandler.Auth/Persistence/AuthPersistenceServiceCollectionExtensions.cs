using Microsoft.EntityFrameworkCore;

namespace WeakAppHandler.Auth.Persistence;

public static class AuthPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddAuthPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Auth")
            ?? throw new InvalidOperationException(
                "Missing required connection string 'ConnectionStrings:Auth'.");

        // Auth/Processor/Notification share one physical database (PRD §4.2's "one role per
        // service", no separate database per service). EF Core's history table name defaults to
        // the same "__EFMigrationsHistory" for every DbContext, so without an explicit, distinct
        // name here, whichever service's role migrates first would create - and therefore
        // exclusively own - a table the other two roles then get "permission denied" writing to
        // (TASK-047, found via a real docker compose run).
        services.AddDbContext<AuthDbContext>(options => options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(AuthDbContext.MigrationsHistoryTableName))
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AuthDbContext>(tags: ["ready"]);

        return services;
    }
}
