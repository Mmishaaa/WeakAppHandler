using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Ingestor.Tests;

// xUnit requires a [CollectionDefinition] to live in the same assembly as the tests referencing it,
// so this thin declaration is repeated per test project. Only the RabbitMQ fixture is shared: the
// Ingestor has no database of its own, and starting PostgreSQL too would be setup with no test
// behind it.
[CollectionDefinition(Name)]
public sealed class IngestionCollectionDefinition : ICollectionFixture<RabbitMqIntegrationFixture>
{
    public const string Name = "Ingestion";
}
