using System.Xml.Linq;

namespace TrainingHub.Architecture.Tests.Framework;

/// <summary>A package reference, and whether it pinned its own version.</summary>
internal sealed record PackageReference(string Name, string? Version);

/// <summary>One project file, read rather than described.</summary>
internal sealed record ProjectFile(
    string Name,
    string RelativePath,
    string? TargetFramework,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<PackageReference> PackageReferences,
    IReadOnlyList<string> FrameworkReferences,
    IReadOnlyDictionary<string, string> Properties)
{
    public bool IsTestProject =>
        Properties.TryGetValue("IsTestProject", out var value)
        && value.Equals("true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this project belongs to one of the two stacks, and to which.</summary>
    public bool BelongsTo(string stack) =>
        Name.StartsWith($"TrainingHub.{stack}.", StringComparison.Ordinal);
}

/// <summary>
/// The solution's project graph, as the csproj files declare it.
/// </summary>
/// <remarks>
/// The strongest fitness function available here, and the one NetArchTest cannot express: three of
/// these projects — DDD.Domain, DDD.Infrastructure and DDDWithCqrs.Domain — compile to assemblies
/// holding no type at all, on purpose, so a type scanner sees nothing to assert on. Their whole
/// contribution to the architecture is an edge in this graph.
/// <para>
/// Reading MSBuild rather than compiled metadata also catches what the compiler prunes. A project
/// reference that no type happens to use disappears from an assembly's referenced-assembly list; it
/// does not disappear from here, and an unused reference between two layers is exactly the kind of
/// thing that becomes a used one later, quietly.
/// </para>
/// </remarks>
internal static class ProjectGraph
{
    public static IReadOnlyList<ProjectFile> Projects { get; } =
    [
        .. SourceTree.ProjectFiles.Select(Read).OrderBy(project => project.Name, StringComparer.Ordinal)
    ];

    /// <summary>Every reference, as a pair of project names.</summary>
    public static IReadOnlyList<(string From, string To)> Edges { get; } =
    [
        .. Projects
            .SelectMany(project => project.ProjectReferences.Select(reference => (From: project.Name, To: reference)))
            .OrderBy(edge => edge.From, StringComparer.Ordinal)
            .ThenBy(edge => edge.To, StringComparer.Ordinal)
    ];

    public static ProjectFile Project(string name) =>
        Projects.SingleOrDefault(project => project.Name == name)
        ?? throw new InvalidOperationException(
            $"No project named '{name}'. The rules name projects so that a rename is a failure here " +
            $"rather than a rule that silently stops applying. Known: " +
            $"{string.Join(", ", Projects.Select(project => project.Name))}.");

    /// <summary>Everything reachable from a project by following references.</summary>
    /// <remarks>Used by the acyclicity rule, and by the rules about what a layer may reach at all.</remarks>
    public static IReadOnlySet<string> Closure(string name)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([name]);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var next in Projects.SingleOrDefault(p => p.Name == current)?.ProjectReferences ?? [])
            {
                if (seen.Add(next))
                {
                    pending.Push(next);
                }
            }
        }

        return seen;
    }

    private static ProjectFile Read(string path)
    {
        var document = XDocument.Load(path);
        var root = document.Root!;

        var properties = root
            .Elements("PropertyGroup")
            .SelectMany(group => group.Elements())
            .GroupBy(element => element.Name.LocalName, StringComparer.Ordinal)
            // Last one wins, as MSBuild would evaluate it.
            .ToDictionary(group => group.Key, group => group.Last().Value.Trim(), StringComparer.Ordinal);

        var items = root.Elements("ItemGroup").SelectMany(group => group.Elements()).ToArray();

        return new ProjectFile(
            Name: Path.GetFileNameWithoutExtension(path),
            RelativePath: SourceTree.Relative(path),
            TargetFramework: properties.GetValueOrDefault("TargetFramework"),
            ProjectReferences:
            [
                .. items
                    .Where(item => item.Name.LocalName == "ProjectReference")
                    .Select(item => Path.GetFileNameWithoutExtension(
                        (item.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/')))
                    .Where(name => name.Length > 0)
                    .OrderBy(name => name, StringComparer.Ordinal)
            ],
            PackageReferences:
            [
                .. items
                    .Where(item => item.Name.LocalName == "PackageReference")
                    .Select(item => new PackageReference(
                        item.Attribute("Include")?.Value ?? string.Empty,
                        item.Attribute("Version")?.Value))
                    .OrderBy(package => package.Name, StringComparer.Ordinal)
            ],
            FrameworkReferences:
            [
                .. items
                    .Where(item => item.Name.LocalName == "FrameworkReference")
                    .Select(item => item.Attribute("Include")?.Value ?? string.Empty)
                    .OrderBy(name => name, StringComparer.Ordinal)
            ],
            Properties: properties);
    }
}
