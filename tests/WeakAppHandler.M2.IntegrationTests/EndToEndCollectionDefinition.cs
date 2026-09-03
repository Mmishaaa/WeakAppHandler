using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.M2.IntegrationTests;

// xUnit requires a [CollectionDefinition] to live in the same test assembly as the tests that
// reference it, so this thin declaration is repeated per test project; the container lifecycle
// logic itself lives once in WeakAppHandler.IntegrationTesting.IntegrationTestFixture.
[CollectionDefinition(Name)]
public sealed class EndToEndCollectionDefinition : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "EndToEnd";
}
