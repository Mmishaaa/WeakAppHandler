namespace WeakAppHandler.IntegrationTesting;

// Composes the Postgres and RabbitMQ fixtures and starts both containers concurrently, since
// starting them sequentially would roughly double fixture setup time for every test class that
// needs both. Reused across M2-M4 via the "Integration" xunit collection below, so each test
// assembly pays the container startup cost once per run, not once per test class.
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    public PostgresIntegrationFixture Postgres { get; } = new();

    public RabbitMqIntegrationFixture RabbitMq { get; } = new();

    public Task InitializeAsync() => Task.WhenAll(Postgres.InitializeAsync(), RabbitMq.InitializeAsync());

    public Task DisposeAsync() => Task.WhenAll(Postgres.DisposeAsync(), RabbitMq.DisposeAsync());
}
