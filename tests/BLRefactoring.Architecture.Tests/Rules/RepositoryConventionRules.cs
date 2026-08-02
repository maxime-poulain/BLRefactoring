using System.Text.RegularExpressions;
using BLRefactoring.Architecture.Tests.Framework;
using Xunit;

namespace BLRefactoring.Architecture.Tests.Rules;

/// <summary>
/// Where a file says it is, against where it actually is.
/// </summary>
/// <remarks>
/// <c>.editorconfig</c> raises IDE0161 to a warning, so every namespace in this repository is
/// file-scoped. It says nothing at all about IDE0130 — whether a namespace agrees with its folder —
/// and so nothing checks the half that a reader actually navigates by. It is true everywhere today,
/// which is exactly when a rule is cheap to write and worth writing.
/// </remarks>
public sealed class RepositoryConventionRules
{
    private static readonly Regex Declaration =
        new(@"^\s*namespace\s+(?<name>[\w.]+)\s*[;{]?\s*$", RegexOptions.Compiled);

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
            // is BLRefactoring.DDD.Api.Controller because the project is BLRefactoring.DDD.Api.csproj:
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
