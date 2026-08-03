using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace TrainingHub.Architecture.Tests.Framework;

/// <summary>
/// The repository as files, for the rules that cannot be answered from compiled metadata.
/// </summary>
/// <remarks>
/// Some decisions leave no trace in an assembly. A namespace that disagrees with its folder, a
/// package name mentioned in a csproj, a <c>using</c> the compiler then pruned — all of them are
/// facts about the tree, not about the build output.
/// <para>
/// The root is stamped in by the csproj as assembly metadata rather than walked for at run time,
/// and it is checked on first use. A root guessed wrong would not fail: it would hand every scan an
/// empty set of files, and an empty set satisfies every rule ever written. That is the one way this
/// suite could go green while checking nothing, so it is the one thing asserted before anything
/// else.
/// </para>
/// </remarks>
internal static class SourceTree
{
    /// <summary>U+FEFF, written as an escape because a literal one is invisible in a diff.</summary>
    private const char ByteOrderMark = '\uFEFF';

    /// <summary>The absolute path of the repository root, with a trailing separator.</summary>
    public static string RepositoryRoot { get; } = ResolveRoot();

    /// <summary>Every <c>.cs</c> file under <c>src/</c> and <c>tests/</c>, build output excluded.</summary>
    public static IReadOnlyList<string> SourceFiles { get; } =
    [
        .. Directories("src", "tests")
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(IsNotBuildOutput)
            .OrderBy(path => path, StringComparer.Ordinal)
    ];

    /// <summary>Every project file in the repository, build output excluded.</summary>
    public static IReadOnlyList<string> ProjectFiles { get; } =
    [
        .. Directories("src", "tests")
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories))
            .Where(IsNotBuildOutput)
            .OrderBy(path => path, StringComparer.Ordinal)
    ];

    /// <summary>
    /// Directories a tool writes into the working tree, and that no commit ever contains.
    /// </summary>
    /// <remarks>
    /// Git's own store, the test runner's reports, the editor's cache — and <c>.sonarqube/</c>,
    /// which is the one that had to be learned. SonarScanner caches the analysis context there as
    /// protobuf, and the SonarCloud project key is part of it, so a scan that counted that file
    /// reported this repository as still carrying a name only SonarCloud carries. A file under any
    /// of these describes the state of a tool, not anything the repository says.
    /// <para>
    /// Declared above the property that reads it, and not below with the other helpers: static
    /// field initialisers run in declaration order, and a set declared afterwards is null by the
    /// time <see cref="AllFiles"/> filters with it.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlySet<string> WorkspaceArtefacts =
        new HashSet<string>(StringComparer.Ordinal) { ".git", ".sonarqube", ".vs", "TestResults" };

    /// <summary>Every file the repository itself contains, workspace artefacts excluded.</summary>
    /// <remarks>
    /// Wider than <see cref="SourceFiles"/> and <see cref="ProjectFiles"/> on purpose: a rule about
    /// what the repository is called has to reach the workflows, the compose file, the records and
    /// the README, none of which are source. Extensions are not filtered: the caller decodes, and
    /// an undecodable byte becomes a replacement character rather than an exception, so an ASCII
    /// name sitting inside something binary is still found instead of being assumed absent.
    /// </remarks>
    public static IReadOnlyList<string> AllFiles { get; } =
    [
        .. Directory
            .EnumerateFiles(RepositoryRoot, "*", SearchOption.AllDirectories)
            .Where(IsNotBuildOutput)
            .Where(IsNotWorkspaceArtefact)
            .OrderBy(path => path, StringComparer.Ordinal)
    ];

    private static readonly IReadOnlyList<Regex> GeneratedCodeGlobs = ReadGeneratedCodeGlobs();

    /// <summary>The path as it would be written in a commit message: repo-relative, forward slashes.</summary>
    public static string Relative(string absolutePath) =>
        Path.GetRelativePath(RepositoryRoot, absolutePath).Replace('\\', '/');

    /// <summary>
    /// Reads a file's lines with the byte-order mark removed.
    /// </summary>
    /// <remarks>
    /// Twenty-seven files in this repository begin with one. <see cref="File.ReadAllLines(string)"/>
    /// detects and strips it already; the explicit trim costs nothing and makes the rules that read
    /// source independent of how a future file happens to be encoded. A regular expression anchored
    /// on <c>^namespace</c> silently misses a file whose first character is U+FEFF, and it misses it
    /// by passing.
    /// </remarks>
    public static string[] ReadLines(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);

        if (lines.Length > 0)
        {
            lines[0] = lines[0].TrimStart(ByteOrderMark);
        }

        return lines;
    }

    /// <summary>Reads a file's text with the byte-order mark removed.</summary>
    public static string ReadText(string path) =>
        File.ReadAllText(path, Encoding.UTF8).TrimStart(ByteOrderMark);

    /// <summary>
    /// Whether a file is generated, and therefore outside every convention about how it is written.
    /// </summary>
    /// <remarks>
    /// Both halves are derived from what the repository already wrote down, rather than restated
    /// here as a second list that can drift from the first: the <c>&lt;auto-generated&gt;</c> header
    /// the generators emit, and the <c>.editorconfig</c> sections that declare
    /// <c>generated_code = true</c>. Today that is the EF migrations and the generated HTTP client.
    /// </remarks>
    public static bool IsGenerated(string path)
    {
        var relative = Relative(path);

        if (GeneratedCodeGlobs.Any(glob => glob.IsMatch(relative)))
        {
            return true;
        }

        return ReadLines(path)
            .Take(3)
            .Any(line => line.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Directories(params string[] names) =>
        names.Select(name => Path.Combine(RepositoryRoot, name)).Where(Directory.Exists);

    private static bool IsNotBuildOutput(string path)
    {
        var relative = Relative(path);

        return !relative.Contains("/bin/", StringComparison.Ordinal)
               && !relative.Contains("/obj/", StringComparison.Ordinal);
    }

    private static bool IsNotWorkspaceArtefact(string path) =>
        !Relative(path).Split('/').Any(WorkspaceArtefacts.Contains);

    private static string ResolveRoot()
    {
        var stamped = typeof(SourceTree).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(metadata => metadata.Key == "RepositoryRoot")
            ?.Value;

        if (string.IsNullOrWhiteSpace(stamped))
        {
            throw new InvalidOperationException(
                "The architecture suite has no RepositoryRoot. It is stamped in by " +
                "TrainingHub.Architecture.Tests.csproj as assembly metadata; without it every " +
                "rule that reads files would run over an empty set and pass.");
        }

        var root = Path.GetFullPath(stamped);

        if (!File.Exists(Path.Combine(root, "TrainingHub.slnx")))
        {
            throw new InvalidOperationException(
                $"RepositoryRoot points at '{root}', which holds no TrainingHub.slnx. A wrong " +
                "root does not fail a file scan — it empties it, and an empty set satisfies every " +
                "rule. Checked here so it fails loudly instead.");
        }

        return root;
    }

    private static IReadOnlyList<Regex> ReadGeneratedCodeGlobs()
    {
        var editorConfig = Path.Combine(RepositoryRoot, ".editorconfig");

        if (!File.Exists(editorConfig))
        {
            return [];
        }

        var globs = new List<Regex>();
        string? section = null;

        foreach (var raw in ReadLines(editorConfig))
        {
            var line = raw.Trim();

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                continue;
            }

            if (section is null || !line.StartsWith("generated_code", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(line.IndexOf("=", StringComparison.Ordinal) + 1)..].Trim();

            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                globs.Add(GlobToRegex(section));
            }
        }

        return globs;
    }

    /// <summary>
    /// Translates the subset of EditorConfig glob syntax this repository actually uses.
    /// </summary>
    /// <remarks>
    /// A pattern holding no separator matches a file name anywhere; one that holds a separator is
    /// anchored at the directory of the <c>.editorconfig</c> that declared it — the repository root,
    /// there being only one.
    /// </remarks>
    private static Regex GlobToRegex(string glob)
    {
        var pattern = new StringBuilder(glob.Contains('/', StringComparison.Ordinal) ? "^" : "^(?:.*/)?");

        for (var index = 0; index < glob.Length; index++)
        {
            var character = glob[index];

            switch (character)
            {
                case '*' when index + 1 < glob.Length && glob[index + 1] == '*':
                    // "**/" spans any number of directories, including none at all.
                    if (index + 2 < glob.Length && glob[index + 2] == '/')
                    {
                        pattern.Append("(?:.*/)?");
                        index += 2;
                    }
                    else
                    {
                        pattern.Append(".*");
                        index++;
                    }

                    break;

                case '*':
                    pattern.Append("[^/]*");
                    break;

                case '?':
                    pattern.Append("[^/]");
                    break;

                case '{':
                    pattern.Append("(?:");
                    break;

                case '}':
                    pattern.Append(')');
                    break;

                case ',':
                    pattern.Append('|');
                    break;

                default:
                    pattern.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }

        pattern.Append('$');

        return new Regex(pattern.ToString(), RegexOptions.IgnoreCase);
    }
}
