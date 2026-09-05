using Microsoft.EntityFrameworkCore;
using WeakAppHandler.IntegrationTesting;
using WeakAppHandler.Processor.Infrastructure.Persistence;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// Builds contexts over the fixture's real PostgreSQL container, configured exactly as
/// <c>AddProcessorInfrastructure</c> configures the production one — the snake-case convention in
/// particular, without which every query would address columns that do not exist.
/// </summary>
internal static class ProcessorDatabase
{
    public static CoreDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTableName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CoreDbContext(options);
    }

    public static async Task<CoreDbContext> CreateMigratedContextAsync(IntegrationTestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var context = CreateContext(fixture.Postgres.ConnectionString);
        await context.Database.MigrateAsync();

        return context;
    }
}
