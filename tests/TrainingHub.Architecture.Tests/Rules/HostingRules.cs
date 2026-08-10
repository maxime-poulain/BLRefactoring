using System.Text;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What it takes for this repository to be runnable by somebody who just cloned it.
/// </summary>
/// <remarks>
/// ADR 0038 recorded the gap these rules close, in as many words: the compose file and the
/// Dockerfile were <em>read by nothing</em>, recorded as known rather than decided. What that cost
/// is measured rather than supposed — the one image this repository had became unbuildable once,
/// because the restore stage stopped copying two files it needs, and nothing noticed until somebody
/// ran <c>docker build</c> by hand.
/// <para>
/// Both failures these rules guard are silent. A host that ships no image is not a broken build,
/// it is a `docker compose up` that starts less than a reader expects; an image nothing builds is
/// not a red pipeline, it is a Dockerfile that rots until the day it is needed. See ADR 0065.
/// </para>
/// </remarks>
public sealed class HostingRules
{
    /// <summary>The compose file the whole stack is described by.</summary>
    private static string Compose { get; } =
        Path.Combine(SourceTree.RepositoryRoot, "docker-compose.yaml");

    /// <summary>The workflow that builds every commit.</summary>
    private static string ContinuousIntegration { get; } =
        Path.Combine(SourceTree.RepositoryRoot, ".github", "workflows", "ci.yml");

    /// <summary>
    /// Every host, ships as an image compose builds.
    /// </summary>
    /// <remarks>
    /// Two halves, and each fails on its own. A Dockerfile beside the composition root is what makes
    /// the host buildable at all; a service naming that Dockerfile is what makes it part of the
    /// stack somebody starts. A repository can hold the first without the second for a long time
    /// and never notice, because everything it does run keeps working.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0065",
        "every host this repository runs ships as an image, and docker compose starts the stack whole")]
    public void EveryHost_ShipsAsAnImage()
    {
        var compose = SourceTree.ReadText(Compose);

        Hosts
            .Selected("host this repository runs")
            .SelectMany(host => Missing(host, compose))
            .ShouldHold();
    }

    /// <summary>
    /// Every image, is built by the pipeline.
    /// </summary>
    /// <remarks>
    /// The rule that answers the sentence the README carried for months — that a <c>docker build</c>
    /// was the check rather than the guarantee. Building without pushing is the whole of it: what
    /// goes wrong in a Dockerfile of this shape is a restore stage that no longer copies what it
    /// needs, and that fails at build time or never.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0065",
        "the pipeline builds every image, so a Dockerfile cannot rot between the days somebody needs it")]
    public void EveryImage_IsBuiltByThePipeline()
    {
        var workflow = SourceTree.ReadText(ContinuousIntegration);

        Hosts
            .Selected("host this repository runs")
            .Select(Image)
            .Where(dockerfile => !workflow.Contains(dockerfile, StringComparison.Ordinal))
            .Select(dockerfile =>
                $"'.github/workflows/ci.yml' never names '{dockerfile}'. Nothing builds that image, " +
                "so it stays correct only for as long as nobody changes the projects it copies — " +
                "which is how the first one in this repository became unbuildable (ADR 0065)")
            .ShouldHold();
    }

    /// <summary>
    /// The image build, carries no layer cache.
    /// </summary>
    /// <remarks>
    /// The inverse of the rule ADR 0067 carried, because the decision inverted on measurement:
    /// warm, the cache saved nothing on the <c>Images</c> step and doubled the job (ADR 0068).
    /// What this rule refuses is the mechanism returning quietly — a <c>--cache</c> flag or a
    /// carried cache directory reappearing without a successor record bringing the warm
    /// measurement 0067 lacked.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0068",
        "the image build carries no layer cache: measured warm, it saved nothing and doubled the " +
        "job, and bringing it back takes a record with a better measurement")]
    public void TheImageBuild_CarriesNoLayerCache()
    {
        var workflow = SourceTree.ReadText(ContinuousIntegration);

        new[]
        {
            (Marker: "--cache-from", Wrong: "imports a layer cache into the image build"),
            (Marker: "--cache-to", Wrong: "exports a layer cache from the image build"),
            (Marker: ".cache/traininghub-images", Wrong: "carries an image layer cache between runs"),
        }
            .Selected("trace of the removed layer cache")
            .Where(trace => workflow.Contains(trace.Marker, StringComparison.Ordinal))
            .Select(trace =>
                $"'.github/workflows/ci.yml' {trace.Wrong} ('{trace.Marker}'). ADR 0068 removed " +
                "the cache on measurement — warm, it saved nothing on the step and doubled the " +
                "job. Bringing it back is a new record's decision, and it owes a warm measurement")
            .ShouldHold();
    }

    /// <summary>
    /// The developer's certificate, never leaves the machine.
    /// </summary>
    /// <remarks>
    /// A PKCS#12 file carries a private key, and ADR 0065 puts it in the family
    /// <c>appsettings.Local.json</c> belongs to: excluded by both ignore files, exactly as ADR 0035
    /// has them exclude the local overrides. This is that consequence made executable, the same
    /// shape as <c>TheLocalOverridesFile_NeverLeavesTheMachine</c> — without it, deleting either
    /// entry stays green until somebody commits a private key.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0065",
        "the certificate joins the family appsettings.Local.json belongs to: git refuses to version " +
        "it and the Docker build context excludes it")]
    public void TheCertificate_NeverLeavesTheMachine() =>
        new[]
        {
            (File: ".gitignore", Entry: "docker/https/*.pfx"),
            (File: ".dockerignore", Entry: "**/*.pfx"),
        }
            .Selected("ignore file")
            .Where(pair => !SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, pair.File))
                .Split('\n')
                .Select(line => line.Trim())
                .Contains(pair.Entry, StringComparer.Ordinal))
            .Select(pair =>
                $"{pair.File} does not list {pair.Entry}. Without that entry a developer's private " +
                "key is one git add or docker build away from leaving the machine")
            .ShouldHold();

    /// <summary>
    /// No folder of source, is hidden from the build context.
    /// </summary>
    /// <remarks>
    /// The failure this one guards has happened twice, in two files that had to learn it
    /// separately. One folder per use case (ADR 0052) means a folder named <c>Release/</c> holding
    /// source, and both ignore files carry a blanket rule for directories of that name — correct for
    /// <c>bin/Release</c> and <c>obj/Release</c>, wrong for a use case. `.gitignore` learned it when
    /// four source files were silently dropped from a commit, and carries the narrow re-inclusion
    /// that fixes it. `.dockerignore` did not, and nothing noticed until an image was built from the
    /// stack that has such a folder.
    /// <para>
    /// A missing file in a build context is not a missing file at compile time — it is a type that
    /// does not exist, reported at the line that uses it, from a Dockerfile that looks correct. So
    /// the rule reads the ignore file the way Docker does, last pattern winning, and asks of every
    /// directory holding source whether it survives.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0065",
        "no folder of source is hidden from the build context: an image is built from what the ignore " +
        "file leaves behind, and a folder named for a build configuration is a use case here")]
    public void NoSourceFolder_IsHiddenFromTheBuildContext()
    {
        var patterns = DockerIgnorePatterns();

        SourceTree.SourceFiles
            .Select(file => SourceTree.Relative(Path.GetDirectoryName(file)!))
            .Where(directory => directory.StartsWith("src/", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(directory => directory, StringComparer.Ordinal)
            .Selected("folder of source")
            .Where(directory => IsExcluded(directory, patterns))
            .Select(directory =>
                $"'.dockerignore' keeps '{directory}' out of the build context, and it holds source " +
                "this repository compiles. An image built without it fails on a type that does not " +
                "exist, at the line that uses it, from a Dockerfile that reads correctly — which is " +
                "why `.gitignore` re-includes the same folder by exact path (ADR 0052, ADR 0065)")
            .ShouldHold();
    }

    /// <summary>
    /// The patterns `.dockerignore` carries, in order, each with whether it re-includes.
    /// </summary>
    private static IReadOnlyList<(Regex Match, bool Negated)> DockerIgnorePatterns() =>
    [
        .. SourceTree.ReadLines(Path.Combine(SourceTree.RepositoryRoot, ".dockerignore"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.StartsWith('!')
                ? (Match: Translate(line[1..]), Negated: true)
                : (Match: Translate(line), Negated: false))
    ];

    /// <summary>
    /// Whether Docker would leave a path out, the last matching pattern deciding.
    /// </summary>
    /// <remarks>
    /// A path is out when it matches an exclusion, and so is everything under a directory that
    /// does — which is why every ancestor is tested and not only the path itself. The last pattern
    /// to match any of them decides, because that is the order Docker resolves them in.
    /// </remarks>
    private static bool IsExcluded(string path, IReadOnlyList<(Regex Match, bool Negated)> patterns)
    {
        var excluded = false;

        foreach (var ancestor in Ancestors(path))
        {
            foreach (var pattern in patterns.Where(pattern => pattern.Match.IsMatch(ancestor)))
            {
                excluded = !pattern.Negated;
            }
        }

        return excluded;
    }

    /// <summary>Each prefix of a path, outermost first: <c>src</c>, <c>src/DDD</c>, and so on.</summary>
    private static IEnumerable<string> Ancestors(string path) =>
        path.Split('/').Select((_, index) => string.Join('/', path.Split('/').Take(index + 1)));

    /// <summary>
    /// The subset of ignore-file glob syntax this repository writes, as an expression.
    /// </summary>
    /// <remarks>
    /// A trailing separator is dropped the way Docker drops it, so a pattern naming a directory
    /// matches that directory. <c>**</c> spans any number of segments; a single <c>*</c> stays
    /// inside one.
    /// </remarks>
    private static Regex Translate(string glob)
    {
        var pattern = new StringBuilder("^");
        var trimmed = glob.TrimEnd('/');

        for (var index = 0; index < trimmed.Length; index++)
        {
            if (trimmed[index] == '*' && index + 1 < trimmed.Length && trimmed[index + 1] == '*')
            {
                // "**/" spans any number of directories, including none at all.
                pattern.Append(index + 2 < trimmed.Length && trimmed[index + 2] == '/' ? "(?:.*/)?" : ".*");
                index += index + 2 < trimmed.Length && trimmed[index + 2] == '/' ? 2 : 1;
            }
            else
            {
                pattern.Append(trimmed[index] switch
                {
                    '*' => "[^/]*",
                    '?' => "[^/]",
                    _ => Regex.Escape(trimmed[index].ToString())
                });
            }
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The directory of every host, derived from the SDK each project declares.
    /// </summary>
    /// <remarks>
    /// Derived rather than written down, for the reason ADR 0041 gives about lists: a second copy
    /// drifts from the first, and a rule reading its own copy holds nothing. The web SDK is the
    /// honest discriminator here — it says a project is a process somebody starts, which is what
    /// makes an image the right unit for it. The WebAssembly client declares
    /// <c>Microsoft.NET.Sdk.BlazorWebAssembly</c> and excludes itself: it has a
    /// <c>Program.cs</c> like the others, but a browser downloads it from the BFF rather than
    /// running it anywhere, so it ships inside that host's image rather than beside it.
    /// </remarks>
    private static IReadOnlyList<string> Hosts { get; } =
    [
        .. SourceTree.ProjectFiles
            .Where(project => SourceTree.Relative(project).StartsWith("src/", StringComparison.Ordinal))
            .Where(project => SourceTree.ReadText(project)
                .Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.Ordinal))
            .Select(project => SourceTree.Relative(Path.GetDirectoryName(project)!))
            .OrderBy(directory => directory, StringComparer.Ordinal)
    ];

    /// <summary>The Dockerfile a host is expected to carry, as compose would name it.</summary>
    private static string Image(string host) => $"{host}/Dockerfile";

    /// <summary>What a host is missing, of the two halves this decision has.</summary>
    private static IEnumerable<string> Missing(string host, string compose)
    {
        var dockerfile = Image(host);

        if (!File.Exists(Path.Combine(SourceTree.RepositoryRoot, dockerfile)))
        {
            yield return
                $"'{host}' is a host of this repository and carries no Dockerfile. Somebody who " +
                $"cloned this repository can start every other host and not this one (ADR 0065)";
        }

        if (!compose.Contains($"dockerfile: {dockerfile}", StringComparison.Ordinal))
        {
            yield return
                $"'docker-compose.yaml' builds no service from '{dockerfile}'. An image nothing " +
                "starts is one a reader has to know about to use, and `docker compose up` is the " +
                "sentence this repository tells them instead (ADR 0065)";
        }
    }
}
