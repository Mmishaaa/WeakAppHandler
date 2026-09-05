using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Auth.Persistence;
using WeakAppHandler.Auth.Persistence.Configurations;
using WeakAppHandler.Auth.Security;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Auth.Tests;

[Collection(IntegrationCollectionDefinition.Name)]
public sealed class AuthDbContextTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_SeedsViewerAndAdminUsers()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var viewer = await context.Users.SingleAsync(u => u.Email == AuthSeedData.ViewerEmail);
        var admin = await context.Users.SingleAsync(u => u.Email == AuthSeedData.AdminEmail);

        Assert.Equal("viewer", viewer.Role);
        Assert.True(Pbkdf2PasswordHasher.Verify(AuthSeedData.ViewerPassword, viewer.PasswordHash));

        Assert.Equal("admin", admin.Role);
        Assert.True(Pbkdf2PasswordHasher.Verify(AuthSeedData.AdminPassword, admin.PasswordHash));
    }

    [Fact]
    public async Task Migrate_AgainstRealPostgresContainer_SeedsServiceClientWithIngestionAdminScope()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var client = await context.ServiceClients.SingleAsync(c => c.ClientId == AuthSeedData.ServiceClientId);

        Assert.Contains(AuthSeedData.ServiceClientScope, client.Scopes);
        Assert.True(Pbkdf2PasswordHasher.Verify(AuthSeedData.ServiceClientSecret, client.ClientSecretHash));
    }

    private AuthDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(
                fixture.Postgres.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable(AuthDbContext.MigrationsHistoryTableName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AuthDbContext(options);
    }
}
