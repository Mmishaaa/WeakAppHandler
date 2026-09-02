using System.Reflection;
using System.Xml.Linq;

namespace WeakAppHandler.Notification.RuleEngine.Tests;

/// <summary>
/// The architectural test TASK-028 asks for: the rule engine has to stay a pure component, because
/// the moment it can reach a DbContext, a clock or a service provider, the boundary cases in
/// <see cref="HysteresisTests"/> and <see cref="CooldownTests"/> stop being arithmetic and start
/// needing infrastructure to reproduce.
/// </summary>
/// <remarks>
/// Two complementary checks, because neither alone is sufficient: the project file says what was
/// declared (the compiler drops unused references from the assembly manifest, so a declared-but-idle
/// dependency would be invisible at runtime), and the loaded assembly says what is actually reached
/// (references inherited from Directory.Build.props never appear in this csproj at all).
/// </remarks>
public sealed class RuleEngineDependencyTests
{
    private static readonly string[] BaseClassLibraryPrefixes = ["System", "netstandard", "mscorlib"];

    [Fact]
    public void RuleEngineProject_DeclaresNoPackageOrProjectReferences()
    {
        XDocument project = XDocument.Load(RuleEngineProjectFile());

        string[] declared = project
            .Descendants()
            .Where(element => element.Name.LocalName
                is "PackageReference" or "ProjectReference" or "Reference" or "FrameworkReference")
            .Select(element => $"{element.Name.LocalName} {element.Attribute("Include")?.Value}")
            .ToArray();

        Assert.Empty(declared);
    }

    [Fact]
    public void RuleEngineAssembly_ReferencesNothingOutsideTheBaseClassLibrary()
    {
        // StyleCop.Analyzers comes in from Directory.Build.props for every project in the repo; it is
        // a compile-time analyzer with PrivateAssets=all and contributes no assembly reference, which
        // is exactly what this assertion confirms rather than assumes.
        AssemblyName[] referenced = typeof(AlertRuleEngine).Assembly.GetReferencedAssemblies();

        string[] outsideBcl = referenced
            .Select(assembly => assembly.Name ?? string.Empty)
            .Where(name => !BaseClassLibraryPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(outsideBcl);
    }

    private static string RuleEngineProjectFile()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WeakAppHandler.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        string projectFile = Path.Combine(
            directory.FullName,
            "src",
            "Notification",
            "WeakAppHandler.Notification.RuleEngine",
            "WeakAppHandler.Notification.RuleEngine.csproj");

        Assert.True(File.Exists(projectFile), $"Expected the rule engine project at {projectFile}.");

        return projectFile;
    }
}
