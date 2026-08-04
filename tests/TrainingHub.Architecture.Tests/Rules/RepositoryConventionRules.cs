using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// Where a file says it is, against where it actually is.
/// </summary>
/// <remarks>
/// <c>.editorconfig</c> raises IDE0161 to a warning, so every namespace in this repository is
/// file-scoped. It says nothing at all about IDE0130 — whether a namespace agrees with its folder —
/// and so nothing checks the half that a reader actually navigates by. It is true everywhere today,
/// which is exactly when a rule is cheap to write and worth writing.
/// </remarks>
public sealed partial class RepositoryConventionRules
{
    [GeneratedRegex(@"^\s*namespace\s+(?<name>[\w.]+)\s*[;{]?\s*$")]
    private static partial Regex Declaration { get; }

    /// <summary>
    /// Every namespace, agrees with its folder.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#repository-conventions",
        "a type's namespace is its project's name followed by its folders, so a reader can navigate by either")]
    public void EveryNamespace_AgreesWithItsFolder() =>
        Sources()
            .Selected("hand-written source file")
            .Where(file => file.Declared is not null && file.Declared != file.Expected)
            .Select(file =>
                $"'{file.Path}' declares namespace {file.Declared}, and its folder says {file.Expected}")
            .ShouldHold();

    /// <summary>
    /// Only entry points, declare no namespace.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#repository-conventions",
        "the only files without a namespace are the entry points, which have top-level statements")]
    public void OnlyEntryPoints_DeclareNoNamespace() =>
        Sources()
            .Selected("hand-written source file")
            .Where(file => file.Declared is null)
            // Named by shape rather than by a list: any file at a project's root called Program.cs is
            // an entry point, and a fifth one appearing anywhere else fails. Both API hosts end
            // theirs with `public partial class Program {}` in the global namespace, which is what
            // WebApplicationFactory<Program> binds to.
            .Where(file => file.Path.Split('/').Length != file.ProjectDepth + 1
                           || !file.Path.EndsWith("/Program.cs", StringComparison.Ordinal))
            .Select(file =>
                $"'{file.Path}' declares no namespace and is not an entry point, so its types land in " +
                "the global namespace where nothing can be said about them")
            .ShouldHold();

    /// <summary>
    /// No project, overrides its root namespace or assembly name.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#repository-conventions",
        "no project renames its own root, which is what lets a folder path predict a namespace at all")]
    public void NoProject_OverridesItsRootNamespaceOrAssemblyName() =>
        ProjectGraph.Projects
            .Selected("project")
            .Where(project => project.Properties.ContainsKey("RootNamespace")
                              || project.Properties.ContainsKey("AssemblyName"))
            .Select(project =>
                $"{project.RelativePath} sets RootNamespace or AssemblyName. Every namespace rule here " +
                "assumes the csproj file name is both, which is what makes them checkable")
            .ShouldHold();

    /// <summary>
    /// Nothing in the repository, still carries the former name.
    /// </summary>
    /// <remarks>
    /// A rename is finished or it is not, and the difference is invisible: a leftover compiles, a
    /// leftover in a comment or a workflow does not even do that, and both read to the next person
    /// as evidence that the name is still in use somewhere they have not looked. This is the rule
    /// that makes "no residual reference" a fact rather than a claim, and it goes on holding for
    /// every file added afterwards.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0022",
        "the former name survives in one place only: the SonarCloud project key, which cannot be changed")]
    public void NothingInTheRepository_StillCarriesTheFormerName() =>
        SourceTree.AllFiles
            .Selected("file in the repository")
            .Where(path => !RecordsOfTheRename.Contains(SourceTree.Relative(path)))
            .Where(path => FormerNameClaimedByThisRepository.IsMatch(File.ReadAllText(path)))
            .Select(path =>
                $"'{SourceTree.Relative(path)}' still writes the name this repository carried before " +
                $"ADR 0022. Rename it to {nameof(TrainingHub)}, unless it is the SonarCloud project " +
                "key — that one names something owned elsewhere and cannot be renamed from here")
            .ShouldHold();

    /// <summary>
    /// The name this repository answered to until ADR 0022, written once so a scan can look for it.
    /// </summary>
    private const string FormerName = "BLRefactoring";

    /// <summary>
    /// An occurrence that belongs to this repository, as opposed to one naming it from outside.
    /// </summary>
    /// <remarks>
    /// <c>maxime-poulain_…</c> is a SonarCloud project key, and a SonarCloud project key is
    /// immutable: changing it means deleting the project and importing another, which discards every
    /// measurement taken against the old one. It is correct where it stands and it is not this
    /// repository's to rename. The GitHub repository was renamed and needs no such exception.
    /// </remarks>
    [GeneratedRegex($"(?<!maxime-poulain_){FormerName}")]
    private static partial Regex FormerNameClaimedByThisRepository { get; }

    /// <summary>
    /// The two files that exist in order to say the former name, and are therefore not leftovers.
    /// </summary>
    /// <remarks>
    /// Listed rather than detected: an exemption a rule works out for itself is one nobody reviews.
    /// The record has to name what was renamed or it records nothing, and this file has to hold the
    /// needle in order to search for it.
    /// </remarks>
    private static readonly IReadOnlySet<string> RecordsOfTheRename =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "docs/adr/0022-name-the-repository-after-the-domain-it-serves.md",
            "tests/TrainingHub.Architecture.Tests/Rules/RepositoryConventionRules.cs"
        };

    /// <summary>A hand-written source file, with the namespace it declares and the one its path implies.</summary>
    private sealed record SourceFile(string Path, string? Declared, string Expected, int ProjectDepth);

    private static IEnumerable<SourceFile> Sources()
    {
        foreach (var absolute in SourceTree.SourceFiles)
        {
            if (SourceTree.IsGenerated(absolute))
            {
                continue;
            }

            var relative = SourceTree.Relative(absolute);
            var project = OwningProject(absolute);

            if (project is null)
            {
                continue;
            }

            var (projectName, projectDirectory) = project.Value;

            // Anchored on the csproj file name, never on the folder path. src/DDD/Api/Controller/
            // is TrainingHub.DDD.Api.Controller because the project is TrainingHub.DDD.Api.csproj:
            // the three folders above it contribute nothing, and a rule that derived the namespace
            // from the repository-relative path would be wrong about every project in both stacks.
            var withinProject = System.IO.Path.GetRelativePath(projectDirectory, System.IO.Path.GetDirectoryName(absolute)!)
                .Replace('\\', '/');

            var expected = withinProject is "."
                ? projectName
                : $"{projectName}.{withinProject.Replace('/', '.')}";

            var declared = SourceTree.ReadLines(absolute)
                .Select(line => Declaration.Match(line))
                .FirstOrDefault(match => match.Success)
                ?.Groups["name"].Value;

            yield return new SourceFile(
                relative,
                declared,
                expected,
                SourceTree.Relative(projectDirectory).Split('/').Length);
        }
    }

    private static (string Name, string Directory)? OwningProject(string file)
    {
        for (var directory = System.IO.Path.GetDirectoryName(file);
             directory is not null && directory.StartsWith(SourceTree.RepositoryRoot, StringComparison.Ordinal);
             directory = System.IO.Path.GetDirectoryName(directory))
        {
            var project = Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault();

            if (project is not null)
            {
                return (System.IO.Path.GetFileNameWithoutExtension(project), directory);
            }
        }

        return null;
    }
}
