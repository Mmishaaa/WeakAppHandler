using System.Runtime.CompilerServices;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using WeakAppHandler.IntegrationTesting;

namespace WeakAppHandler.Gateway.Api.Tests;

/// <summary>
/// TASK-025's schema-drift check: the SDL the live server actually builds must match the file
/// committed at <c>src/Gateway/WeakAppHandler.Gateway.Api/schema.graphql</c>. There is no CI
/// pipeline yet for this repository (TASK-049 adds one) - until then, this test IS the check the
/// acceptance criterion asks for, and TASK-049 only has to point a workflow at it.
/// </summary>
[Collection(IntegrationCollectionDefinition.Name)]
public sealed class SchemaSnapshotTests(IntegrationTestFixture fixture)
{
    private const string ReceivedSuffix = ".received";

    [Fact]
    public async Task Schema_MatchesTheCommittedSnapshot()
    {
        using var factory = GatewayApiFactory.Create(fixture.Postgres.ConnectionString);

        // Forces the host (and the schema build it does lazily) the same way NotificationHost's
        // WaitUntilStarted probe does - resolving the executor is what makes it exist.
        var executorProvider = factory.Services.GetRequiredService<IRequestExecutorProvider>();
        var schemaName = executorProvider.SchemaNames[0];
        var executor = await executorProvider.GetExecutorAsync(schemaName);

        var actual = Normalize(executor.Schema.ToString());
        var path = SchemaFilePath();

        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path + ReceivedSuffix, actual);
            Assert.Fail(
                $"{path} does not exist yet. The live schema was written to {path}{ReceivedSuffix} - " +
                "review it and commit it at that path (without the suffix) to establish the snapshot.");
        }

        var expected = Normalize(await File.ReadAllTextAsync(path));

        if (expected == actual)
        {
            // A stale .received file from a previous failing run would otherwise sit in the working
            // tree looking like an uncommitted change.
            File.Delete(path + ReceivedSuffix);
            return;
        }

        await File.WriteAllTextAsync(path + ReceivedSuffix, actual);
        Assert.Fail(
            $"The live GraphQL schema no longer matches {path}. The current schema was written to " +
            $"{path}{ReceivedSuffix} for diffing - if the change is intentional, replace the committed " +
            "file with it; otherwise the schema changed by accident.");
    }

    /// <summary>Newline and trailing-whitespace differences are not schema drift.</summary>
    private static string Normalize(string sdl) => sdl.ReplaceLineEndings("\n").Trim();

    /// <summary>
    /// <see cref="CallerFilePathAttribute"/> embeds this file's own absolute path at compile time, on
    /// whichever machine does the compiling - so this resolves correctly from a developer's checkout
    /// and from a CI runner's without either needing to match the other, and without depending on the
    /// test host's current working directory or bin output layout.
    /// </summary>
    private static string SchemaFilePath([CallerFilePath] string thisFilePath = "")
    {
        // thisFilePath: .../tests/WeakAppHandler.Gateway.Api.Tests/SchemaSnapshotTests.cs
        var testProjectDir = Path.GetDirectoryName(thisFilePath)!;
        var repoRoot = Path.GetFullPath(Path.Combine(testProjectDir, "..", ".."));

        return Path.Combine(
            repoRoot, "src", "Gateway", "WeakAppHandler.Gateway.Api", "schema.graphql");
    }
}
