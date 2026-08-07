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
public sealed class PipelineRules
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
        "one run per commit: a branch of this repository is built by its push, a fork by its pull request")]
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
                "builds the commit. A branch of this repository triggers each once a pull request " +
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
    /// being true whenever the push run is cancelled without reaching a runner, which this
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
             Wrong: "analyses no pull request, so a regression is found after it is merged"),
            (Setting: "branches: [master]",
             Wrong: "analyses no push to master, so there is no baseline for a pull request to be " +
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
}
