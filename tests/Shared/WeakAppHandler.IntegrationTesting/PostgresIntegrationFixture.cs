using Testcontainers.PostgreSql;

namespace WeakAppHandler.IntegrationTesting;

public sealed class PostgresIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("weakapphandler")
        .WithUsername("weakapphandler")
        .WithPassword("weakapphandler")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
