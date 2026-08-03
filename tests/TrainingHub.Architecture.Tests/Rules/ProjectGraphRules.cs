using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What the solution's project files declare, against what the README says they declare.
/// </summary>
/// <remarks>
/// These rules read MSBuild, not metadata, for two reasons. Three projects — DDD.Domain,
/// DDD.Infrastructure and DDDWithCqrs.Domain — hold no source file on purpose, so they compile to
/// assemblies with nothing in them and a type scanner has nothing to say about them; their entire
/// contribution to the architecture is an edge in this graph. And the compiler prunes a project
/// reference no type happens to use, so a layering mistake can be invisible in the build output
/// while sitting in plain sight in the csproj.
/// </remarks>
public sealed class ProjectGraphRules
{
    /// <summary>
    /// The diagram, describes exactly the edges the projects declare.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#project-dependency-graph",
        "the diagram is the graph: every edge the projects declare is drawn, and every edge drawn exists")]
    public void TheDiagram_DescribesExactlyTheEdgesTheProjectsDeclare()
    {
        // The diagram is about the product, so the comparison is too: a test project referencing
        // half the solution is not an architectural fact, and drawing it would bury the graph that
        // matters under the graph that does not.
        var underSrc = ProjectGraph.Projects
            .Where(project => project.RelativePath.StartsWith("src/", StringComparison.Ordinal))
            .Select(project => project.Name)
            .ToHashSet(StringComparer.Ordinal);

        var declared = ProjectGraph.Edges
            .Where(edge => underSrc.Contains(edge.From))
            .Select(edge => $"{edge.From} -> {edge.To}")
            .ToHashSet(StringComparer.Ordinal);

        var drawn = MermaidGraph.Edges.Selected("edge in the README diagram");

        var violations = drawn
            .Where(edge => !declared.Contains(edge))
            .Select(edge => $"the README draws '{edge}', which no project file declares")
            .Concat(declared
                .Where(edge => !drawn.Contains(edge))
                .Select(edge => $"'{edge}' is declared by a project file and missing from the README diagram"));

        violations.ShouldHold();
    }

    /// <summary>
    /// The domain, references the kernel and nothing else.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "the domain references exactly one project — the shared kernel — and nothing else")]
    public void TheDomain_ReferencesTheKernelAndNothingElse() =>
        ProjectGraph.Project("TrainingHub.Shared.Domain").ProjectReferences
            .Selected("reference on the domain project")
            .Where(reference => reference != "TrainingHub.Shared")
            .Select(reference => $"TrainingHub.Shared.Domain references '{reference}'")
            .ShouldHold();

    /// <summary>
    /// The kernel, references no project.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "the kernel is the root of the graph: it references no project of this solution")]
    public void TheKernel_ReferencesNoProject() =>
        ProjectGraph.Project("TrainingHub.Shared").ProjectReferences
            .Select(reference => $"the kernel references '{reference}', so it is no longer the root of anything")
            .ShouldHold();

    /// <summary>
    /// No application layer, references infrastructure or a host.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "an application layer never reaches outward: not to infrastructure, not to a host")]
    public void NoApplicationLayer_ReferencesInfrastructureOrAHost() =>
        ProjectGraph.Projects
            .Where(project => project.Name.EndsWith(".Application", StringComparison.Ordinal))
            .Selected("application project")
            .SelectMany(project => project.ProjectReferences
                .Where(reference => reference.EndsWith(".Infrastructure", StringComparison.Ordinal)
                                    || reference.EndsWith(".Api", StringComparison.Ordinal))
                .Select(reference => $"{project.Name} references '{reference}'"))
            .ShouldHold();

    /// <summary>
    /// Neither stack, references the other.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#two-stacks-one-domain",
        "the two stacks are a comparison, so neither may lean on the other")]
    public void NeitherStack_ReferencesTheOther() =>
        ProjectGraph.Projects
            .Selected("project")
            .SelectMany(project => project.ProjectReferences
                .Where(reference =>
                    (project.BelongsTo("DDD") && reference.StartsWith("TrainingHub.DDDWithCqrs.", StringComparison.Ordinal))
                    || (project.BelongsTo("DDDWithCqrs") && reference.StartsWith("TrainingHub.DDD.", StringComparison.Ordinal)))
                .Select(reference => $"{project.Name} references '{reference}', across the two stacks"))
            .ShouldHold();

    /// <summary>
    /// The graph, has no cycle.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#project-dependency-graph",
        "the graph is a directed acyclic graph, as any build order requires")]
    public void TheGraph_HasNoCycle() =>
        ProjectGraph.Projects
            .Selected("project")
            .Where(project => ProjectGraph.Closure(project.Name).Contains(project.Name))
            .Select(project => $"{project.Name} can reach itself by following references")
            .ShouldHold();

    /// <summary>
    /// Only the shared api layer, carries the web framework reference.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "persistence carries no ASP.NET Core framework reference; only the shared API layer does")]
    public void OnlyTheSharedApiLayer_CarriesTheWebFrameworkReference() =>
        ProjectGraph.Projects
            .Where(project => project.Name != "TrainingHub.Shared.Api")
            .Selected("project outside the shared API layer")
            .Where(project => project.FrameworkReferences.Contains("Microsoft.AspNetCore.App"))
            // The two hosts and the Blazor pair get it from their SDK, which is a different thing:
            // this rule is about a class library asking for the whole web framework by name.
            .Where(project => !project.Name.EndsWith(".Api", StringComparison.Ordinal)
                              && !project.Name.StartsWith("TrainingHub.Blazor", StringComparison.Ordinal)
                              && !project.Name.StartsWith("TrainingHub.Architecture", StringComparison.Ordinal))
            .Select(project => $"{project.RelativePath} asks for Microsoft.AspNetCore.App by name")
            .ShouldHold();

    /// <summary>
    /// No project, pins its own package version.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#repository-conventions",
        "package versions live in Directory.Packages.props, and nowhere else")]
    public void NoProject_PinsItsOwnPackageVersion() =>
        ProjectGraph.Projects
            .SelectMany(project => project.PackageReferences.Select(package => (project, package)))
            .Selected("package reference")
            .Where(pair => pair.package.Version is not null)
            .Select(pair =>
                $"{pair.project.RelativePath} pins {pair.package.Name} at {pair.package.Version} " +
                "instead of leaving the version to central management")
            .ShouldHold();

    /// <summary>
    /// The generated client, depends on nothing.
    /// </summary>
    [Fact]
    [ArchitectureRule("0008",
        "the generated client is generated: it holds one file and depends on nothing")]
    public void TheGeneratedClient_DependsOnNothing()
    {
        var client = ProjectGraph.Project("TrainingHub.GeneratedClients");

        client.ProjectReferences
            .Select(reference => $"the generated client references the project '{reference}'")
            .Concat(client.PackageReferences
                .Select(package => $"the generated client references the package '{package.Name}'"))
            .ShouldHold();
    }

    /// <summary>
    /// No project, brings back a second open api generator.
    /// </summary>
    [Fact]
    [ArchitectureRule("0006",
        "the document comes from the framework's own generator, and from no second one")]
    public void NoProject_BringsBackASecondOpenApiGenerator()
    {
        string[] displaced = ["NSwag.MSBuild", "NSwag.AspNetCore", "Swashbuckle.AspNetCore"];

        ProjectGraph.Projects
            .SelectMany(project => project.PackageReferences.Select(package => (project, package)))
            .Selected("package reference")
            .Where(pair => displaced.Contains(pair.package.Name, StringComparer.Ordinal))
            .Select(pair =>
                $"{pair.project.RelativePath} references {pair.package.Name}. The document is produced " +
                "by Microsoft.AspNetCore.OpenApi; a second generator means two descriptions of one API, " +
                "and the client is generated from whichever one the script happened to read")
            .ShouldHold();
    }

    /// <summary>
    /// Only a test project, spans the two target frameworks.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#repository-conventions",
        "the backend is net10.0 and the browser pair is net9.0; only a test may span the two")]
    public void OnlyATestProject_SpansTheTwoTargetFrameworks() =>
        ProjectGraph.Projects
            .Where(project => project.TargetFramework is not null)
            .Selected("project declaring a target framework")
            .SelectMany(project => project.ProjectReferences
                .Select(reference => (project, referenced: ProjectGraph.Project(reference)))
                .Where(pair => pair.referenced.TargetFramework is not null
                               && pair.referenced.TargetFramework != pair.project.TargetFramework)
                .Where(pair => !pair.project.IsTestProject)
                .Select(pair =>
                    $"{pair.project.Name} ({pair.project.TargetFramework}) references " +
                    $"{pair.referenced.Name} ({pair.referenced.TargetFramework})"))
            .ShouldHold();

    /// <summary>
    /// Every test project, declares whether it is one.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#testing",
        "the shared kit is not a test project, and every real suite says that it is")]
    public void EveryTestProject_DeclaresWhetherItIsOne() =>
        ProjectGraph.Projects
            .Where(project => project.RelativePath.StartsWith("tests/", StringComparison.Ordinal))
            .Selected("project under tests/")
            .Where(project => !project.Properties.ContainsKey("IsTestProject"))
            .Select(project =>
                $"{project.RelativePath} declares no IsTestProject. Left unset, VSTest tries to run " +
                "the assembly and aborts the run when it holds no tests")
            .ShouldHold();
}

/// <summary>
/// The README's dependency diagram, parsed.
/// </summary>
/// <remarks>
/// Mermaid declares a node as <c>Alias["Label"]</c> and an edge as <c>Alias --&gt; Alias</c>. The
/// labels are written short — <c>Shared.Domain</c>, not <c>TrainingHub.Shared.Domain</c> — so the
/// prefix every project name carries is put back before comparing.
/// </remarks>
internal static class MermaidGraph
{
    private static readonly Regex Node = new(@"^\s*(?<alias>\w+)\[""(?<label>[^""]+)""\]", RegexOptions.Compiled);

    private static readonly Regex Edge = new(@"^\s*(?<from>\w+)\s*-->\s*(?<to>\w+)\s*$", RegexOptions.Compiled);

    /// <summary>The drawn edges, as "&lt;project&gt; -&gt; &lt;project&gt;".</summary>
    public static IReadOnlySet<string> Edges { get; } = Read();

    private static IReadOnlySet<string> Read()
    {
        var lines = SourceTree.ReadLines(Path.Combine(SourceTree.RepositoryRoot, "README.md"));

        var block = lines
            .SkipWhile(line => !line.Contains("### Project dependency graph", StringComparison.Ordinal))
            .Skip(1)
            .SkipWhile(line => !line.StartsWith("```mermaid", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("```", StringComparison.Ordinal))
            .ToArray();

        var aliases = block
            .Select(line => Node.Match(line))
            .Where(match => match.Success)
            .ToDictionary(
                match => match.Groups["alias"].Value,
                match => Qualify(match.Groups["label"].Value),
                StringComparer.Ordinal);

        return block
            .Select(line => Edge.Match(line))
            .Where(match => match.Success)
            .Select(match => $"{aliases[match.Groups["from"].Value]} -> {aliases[match.Groups["to"].Value]}")
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Qualify(string label) =>
        label.StartsWith("TrainingHub.", StringComparison.Ordinal) ? label : $"TrainingHub.{label}";
}
