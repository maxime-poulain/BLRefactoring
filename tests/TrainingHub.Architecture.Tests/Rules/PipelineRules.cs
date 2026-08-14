using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What the pipeline has to keep true to be worth running.
/// </summary>
/// <remarks>
/// Every rule here guards a failure that is <em>green</em>. A test project with no coverage
/// collector reports nothing rather than failing, and the gate reads the silence as uncovered code;
/// a generated file that stops being excluded is measured like hand-written code, and the
/// duplication figure becomes a statement about NSwag's output; a workflow that builds the same
/// commit twice answers correctly, at twice the price. None of them is visible in a diff and none
/// makes a run red — they make the numbers wrong or the bill high, and a wrong number is still
/// believed.
/// </remarks>
public sealed partial class PipelineRules
{
    private static string Workflow { get; } =
        Path.Combine(SourceTree.RepositoryRoot, ".github", "workflows", "sonar.yml");

    private static string ContinuousIntegration { get; } =
        Path.Combine(SourceTree.RepositoryRoot, ".github", "workflows", "ci.yml");

    /// <summary>
    /// Every test project, collects coverage.
    /// </summary>
    [Fact]
    [ArchitectureRule("0017",
        "a project that runs tests collects coverage, or the gate reads its silence as zero")]
    public void EveryTestProject_CollectsCoverage() =>
        SourceTree.ProjectFiles
            .Select(project => (project, text: SourceTree.ReadText(project)))
            .Where(pair => pair.text.Contains("Microsoft.NET.Test.Sdk", StringComparison.Ordinal))
            .Selected("project that runs tests")
            .Where(pair => !pair.text.Contains("coverlet.collector", StringComparison.Ordinal))
            .Select(pair =>
                $"'{SourceTree.Relative(pair.project)}' runs tests and references no coverage " +
                "collector. Its lines would be reported as covered by nothing at all, which the " +
                "quality gate cannot tell apart from code nobody tested")
            .ShouldHold();

    /// <summary>
    /// The build, does not run twice for one commit.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#continuous-integration",
        "one run per commit: a claude/** branch stands on its push run's verdict, and everything " +
        "else is built by its pull-request run")]
    public void TheBuild_DoesNotRunTwiceForOneCommit()
    {
        // ci.yml by name rather than every workflow, because the defect is an overlap between two
        // branch sets rather than the presence of two triggers. sonar.yml declares both and needs
        // no guard: it pushes only on master, and a pull request's head branch is never master.
        // Telling those two apart means resolving branch patterns, which a scan cannot do — so the
        // one file where the sets do overlap is named here.
        var text = SourceTree.ReadText(ContinuousIntegration);

        // The step that decides, rather than the job-level condition that used to. ADR 0047 moved
        // the choice inside the job so the check is always produced by something that ran; what
        // this rule holds is unchanged — that the overlap is still resolved somewhere, and that
        // deleting the resolution costs a build twice rather than passing unnoticed.
        const string Delegation = "id: delegation";

        new[] { SourceTree.Relative(ContinuousIntegration) }
            .Selected("workflow whose triggers overlap")
            .Where(_ =>
                text.Contains("  push:", StringComparison.Ordinal) &&
                text.Contains("  pull_request:", StringComparison.Ordinal) &&
                !text.Contains(Delegation, StringComparison.Ordinal))
            .Select(file =>
                $"'{file}' fires on both push and pull_request without deciding which of the two " +
                "builds the commit. A claude/** branch triggers each once a pull request " +
                "is open, and the two runs land in different concurrency groups, so neither cancels " +
                "the other and one build is paid for twice")
            .ShouldHold();
    }

    /// <summary>
    /// No delegated build, is taken on trust.
    /// </summary>
    /// <remarks>
    /// The green failure this one guards is the worst kind: a pull request whose check says a build
    /// passed when none ran. A job skipped by a job-level condition still posts its check, and
    /// GitHub counts a skipped check as passing — so the sentence "its commit was already built when
    /// it was pushed" was load-bearing while being a claim about a run nothing looked at. It stops
    /// being true whenever the push run is canceled without reaching a runner, which this
    /// repository has measured on its own default branch.
    /// <para>
    /// So the job must run in every case, and where it delegates it must read the other run's
    /// conclusion. Two things are asserted, and the second is the one that matters: any
    /// <c>if:</c> at the job's own level would bring the skipped check back, and the delegation must
    /// consult <c>conclusion</c> rather than merely observe that a run exists. A run that exists and
    /// failed is exactly the case ADR 0047 was written for.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0047",
        "a check on a pull request is green only for a build that happened: where the build is delegated, " +
        "the delegating job waits for that build and adopts its verdict")]
    public void NoDelegatedBuild_IsTakenOnTrust()
    {
        var text = SourceTree.ReadText(ContinuousIntegration);

        // The job's own condition sits at four spaces; a step's sits at eight. Matching the indent
        // is what tells "this job may be skipped entirely" apart from "this step may be".
        var skipsTheJob = text.Contains("\n    if:", StringComparison.Ordinal);

        new[]
        {
            (Broken: skipsTheJob,
             Wrong: "carries a job-level `if:` on the build. A job skipped that way still posts its " +
                    "check, and GitHub reads a skipped check as a passing one — which is a green " +
                    "answer for a build that never ran"),
            (Broken: !text.Contains("head_sha=$HEAD_SHA", StringComparison.Ordinal),
             Wrong: "does not look the delegated build up by the commit under review, so whatever it " +
                    "reads is about some other commit"),
            (Broken: !text.Contains("completed success", StringComparison.Ordinal),
             Wrong: "does not require the delegated build to have concluded successfully. A run that " +
                    "exists and failed would pass for one that worked")
        }
            .Selected("condition on the delegated build")
            .Where(assertion => assertion.Broken)
            .Select(assertion => $"'.github/workflows/ci.yml' {assertion.Wrong} (ADR 0047)")
            .ShouldHold();
    }

    /// <summary>
    /// The gate, is waited on for a pull request and not for the default branch.
    /// </summary>
    [Fact]
    [ArchitectureRule("0018",
        "the gate fails the build where failing stops something, and reports where it would not")]
    public void TheGate_IsWaitedOnForAPullRequestAndNotForTheDefaultBranch()
    {
        var text = SourceTree.ReadText(Workflow);

        new[]
        {
            (Setting: "sonar.qualitygate.wait=${{ github.event_name == 'pull_request' }}",
             Present: true,
             Wrong: "does not make the gate wait depend on the event. On a pull request the wait is " +
                    "what stops a failing change entering master; on a push to master the same " +
                    "failure stops nothing and only paints the default branch red"),
            (Setting: "sonar.qualitygate.wait=true",
             Present: false,
             Wrong: "waits on the gate unconditionally, so a verdict on already-merged code is " +
                    "published as a broken build")
        }
            .Selected("condition on the gate wait")
            .Where(rule => text.Contains(rule.Setting, StringComparison.Ordinal) != rule.Present)
            .Select(rule => $"'.github/workflows/sonar.yml' {rule.Wrong}")
            .ShouldHold();
    }

    /// <summary>
    /// The analysis, covers the pull request and the default branch.
    /// </summary>
    [Fact]
    [ArchitectureRule("0017",
        "the analysis reads both the pull request and the branch it targets, or there is nothing to compare")]
    public void TheAnalysis_CoversThePullRequestAndTheDefaultBranch()
    {
        var text = SourceTree.ReadText(Workflow);

        new[]
        {
            (Setting: "pull_request:",
             Wrong: "analyzes no pull request, so a regression is found after it is merged"),
            (Setting: "branches: [master]",
             Wrong: "analyzes no push to master, so there is no baseline for a pull request to be " +
                    "compared against"),
            (Setting: "src/TrainingHub.GeneratedClients/**",
             Wrong: "measures the generated client like code somebody wrote — two thousand four " +
                    "hundred lines of NSwag output deciding the duplication figure"),
            (Setting: "**/Migrations/**",
             Wrong: "measures the EF Core migrations like code somebody wrote — two thousand five " +
                    "hundred lines, a fifth of this repository's production code, that nobody " +
                    "writes and nothing covers"),
            (Setting: "sonar.cs.vstest.reportsPaths",
             Wrong: "publishes no test results, so the dashboard reports zero tests for a " +
                    "repository whose argument is its test suite")
        }
            .Selected("setting the analysis depends on")
            .Where(rule => !text.Contains(rule.Setting, StringComparison.Ordinal))
            .Select(rule => $"'.github/workflows/sonar.yml' {rule.Wrong}")
            .ShouldHold();
    }

    /// <summary>
    /// The corpora exempt from the duplication measure for a reason other than being a host.
    /// </summary>
    /// <remarks>
    /// Exempt <em>by name</em>, in the mold ADR 0066 sets for everything this repository excuses:
    /// nothing is exempt by being forgotten, and each entry carries the argument it was admitted on.
    /// A path here is a file whose repetition is its data — a table written in the language rather
    /// than beside it — and not one whose repetition somebody has not got round to removing. The
    /// distinction is the whole of ADR 0049, and it is why this is a registry of two fields instead
    /// of a longer string in a workflow.
    /// </remarks>
    private static readonly (string Path, string Why)[] WrittenCorpora =
    [
        ("src/TrainingHub.Shared.Infrastructure/Development/DevelopmentCatalog.cs",
         "ninety-six training blueprints across twelve areas of expertise, each a record of the " +
         "same shape carrying different prose (ADR 0079)")
    ];

    /// <summary>
    /// The duplication measure, excludes the hosts written twice and nothing else.
    /// </summary>
    /// <remarks>
    /// This repository publishes one API from two implementations, so the routing attribute, the
    /// <c>[ProducesResponseType]</c> run and the action signature of every endpoint appear twice —
    /// identically, because <c>BothHosts_PublishTheSameOperations</c> and
    /// <c>BothHosts_AnswerEachOperationWithTheSameShape</c> require it. A copy-paste detector reads
    /// that as duplication, and it is not wrong about the text: it is wrong about what the text is.
    /// <para>
    /// So the exemption exists, and this rule is what keeps it honest. It holds three things that
    /// fail separately. Every host is <em>covered</em>, derived from <see cref="Solution.Hosts"/>
    /// rather than spelled here, so a third host cannot arrive without the exclusion following it.
    /// It is the <em>duplication</em> measure that is relaxed and not the analysis — a host path in
    /// <c>sonar.exclusions</c> would stop measuring bugs and coverage on the busiest files in the
    /// repository to settle a duplication figure. And the exemption does not <em>grow</em>: anything
    /// in <c>sonar.cpd.exclusions</c> that is neither a host nor a corpus this repository named is a
    /// suppression wearing this record's clothes.
    /// </para>
    /// <para>
    /// The third clause is the one worth the rule. Without it this is a line in a workflow that
    /// anybody can lengthen by one path the next time a figure is inconvenient, which is the move
    /// <c>CLAUDE.md</c> forbids: never act on a finding to make it stop reporting. See ADR 0049.
    /// </para>
    /// <para>
    /// ADR 0079 widened it by exactly one category, and the widening is what the fourth clause is
    /// for. A written corpus is a table that happens to be in C#: the shape repeats because it is
    /// the shape, and no arrangement inside the language changes that. So corpora are admitted —
    /// through <see cref="WrittenCorpora"/>, where each carries its argument — and a registered path
    /// that names no file fails, so the registry cannot rot into a wildcard by outliving what it
    /// described.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0049",
        "duplication is measured where repetition is a defect: the two hosts are exempt because two rules " +
        "require their published declarations to be identical, a written corpus is exempt because its " +
        "shape is its data (ADR 0079), and nothing else is")]
    public void TheDuplicationMeasure_ExcludesTheHostsWrittenTwiceAndNothingElse()
    {
        var text = SourceTree.ReadText(Workflow);
        var exempt = Excluded(text, "sonar.cpd.exclusions");
        var unanalyzed = Excluded(text, "sonar.exclusions");

        var hosts = HostProjects.Selected("host the API is written in");

        hosts
            .Where(host => !exempt.Contains(Everything(host), StringComparison.Ordinal))
            .Select(host =>
                $"'{Everything(host)}' is missing from sonar.cpd.exclusions. Every endpoint under it " +
                "is the second copy of its twin, and two rules require the two copies to declare the " +
                "same operations and the same shapes — so the gate would fail on any change touching " +
                "both hosts, for repetition this repository decided on")
            .Concat(hosts
                .Where(host => unanalyzed.Contains(host, StringComparison.Ordinal))
                .Select(host =>
                    $"'{host}' is named in sonar.exclusions, which removes it from the analysis " +
                    "altogether. What ADR 0049 exempts is the duplication measure alone: bugs, " +
                    "hotspots and coverage on a host's controllers are exactly what nobody wants to " +
                    "stop reading"))
            .Concat(exempt
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(path => !hosts.Any(host =>
                    string.Equals(path, Everything(host), StringComparison.Ordinal)))
                .Where(path => !WrittenCorpora.Any(corpus =>
                    string.Equals(path, corpus.Path, StringComparison.Ordinal)))
                .Select(path =>
                    $"'{path}' is exempt from the duplication measure, and is neither a host of this " +
                    "API nor a corpus this repository named. ADR 0049 exempts the code written twice " +
                    "on purpose and ADR 0079 the tables written in C#; anything else under that " +
                    "setting is a finding being silenced rather than answered"))
            .Concat(WrittenCorpora
                .Where(corpus => !File.Exists(Path.Combine(
                    SourceTree.RepositoryRoot, corpus.Path.Replace('/', Path.DirectorySeparatorChar))))
                .Select(corpus =>
                    $"'{corpus.Path}' is registered as a written corpus — {corpus.Why} — and no such " +
                    "file exists. An exemption that outlives what it described stops naming anything, " +
                    "and the next reader has no way to tell it from a wildcard"))
            .ShouldHold();
    }

    /// <summary>The glob covering everything a project holds.</summary>
    private static string Everything(string project) => $"{project}/**";

    /// <summary>
    /// The directory of each host's project, relative to the root and derived from the solution.
    /// </summary>
    /// <remarks>
    /// From <see cref="Solution.Hosts"/> rather than written down, for the reason ADR 0041 gives: a
    /// list restated in a rule is a second copy that drifts from the first, and a rule reading its
    /// own copy holds nothing.
    /// </remarks>
    private static IReadOnlyList<string> HostProjects { get; } =
    [
        .. Solution.Hosts
            .Select(host => host.GetName().Name)
            .Select(name => SourceTree.ProjectFiles.Single(project =>
                string.Equals(Path.GetFileNameWithoutExtension(project), name, StringComparison.Ordinal)))
            .Select(project => SourceTree.Relative(Path.GetDirectoryName(project)!))
            .OrderBy(path => path, StringComparer.Ordinal)
    ];

    /// <summary>The comma-separated value a scanner setting carries, or empty when it is absent.</summary>
    private static string Excluded(string workflow, string setting) =>
        ScannerSetting
            .Matches(workflow)
            .Where(match => string.Equals(match.Groups["name"].Value, setting, StringComparison.Ordinal))
            .Select(match => match.Groups["value"].Value)
            .FirstOrDefault(string.Empty);

    /// <summary>A setting handed to <c>sonarscanner begin</c>, with the value it carries.</summary>
    [GeneratedRegex(@"/d:(?<name>sonar(?:\.\w+)+)=""(?<value>[^""]*)""")]
    private static partial Regex ScannerSetting { get; }
}
