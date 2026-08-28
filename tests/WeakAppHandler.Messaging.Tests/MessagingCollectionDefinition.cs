using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Messaging.Tests;

// xUnit requires a [CollectionDefinition] to live in the same test assembly as the tests that
// reference it, so this thin declaration is repeated per test project. Only the RabbitMQ fixture is
// shared here rather than the composed IntegrationTestFixture: nothing in this assembly touches
// PostgreSQL, and starting a database container as well would be setup cost with no test behind it.
[CollectionDefinition(Name)]
public sealed class MessagingCollectionDefinition : ICollectionFixture<RabbitMqIntegrationFixture>
{
    public const string Name = "Messaging";
}
