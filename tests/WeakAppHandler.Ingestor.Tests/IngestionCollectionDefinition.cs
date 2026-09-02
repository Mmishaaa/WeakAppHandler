using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Ingestor.Tests;

// xUnit requires a [CollectionDefinition] to live in the same assembly as the tests referencing it,
// so this thin declaration is repeated per test project. Both containers are shared: the Ingestor
// itself has no database, but its admin API (TASK-017) is guarded by machine tokens that only the
// real Auth Service can mint, and that service does have one.
[CollectionDefinition(Name)]
public sealed class IngestionCollectionDefinition : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "Ingestion";
}
