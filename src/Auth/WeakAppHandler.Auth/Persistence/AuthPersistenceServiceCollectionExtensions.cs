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

        services.AddDbContext<AuthDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AuthDbContext>(tags: ["ready"]);

        return services;
    }
}
