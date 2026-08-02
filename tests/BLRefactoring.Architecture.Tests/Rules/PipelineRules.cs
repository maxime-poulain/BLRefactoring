using BLRefactoring.Architecture.Tests.Framework;
using Xunit;

namespace BLRefactoring.Architecture.Tests.Rules;

/// <summary>
/// What the pipeline has to keep true for the analysis to mean anything.
/// </summary>
/// <remarks>
/// Both of these guard a failure that is green. A test project with no coverage collector reports
/// nothing rather than failing, and the gate reads the silence as uncovered code; a generated file
/// that stops being excluded is measured like hand-written code, and the duplication figure becomes
/// a statement about NSwag's output. Neither is visible in a diff, and neither makes a run red —
/// they make the numbers wrong, which is worse, because a wrong number is still believed.
/// </remarks>
public sealed class PipelineRules
{
    private static string Workflow { get; } =
        Path.Combine(SourceTree.RepositoryRoot, ".github", "workflows", "sonar.yml");

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

    [Fact]
    [ArchitectureRule("0017",
        "the analysis reads both the pull request and the branch it targets, or there is nothing to compare")]
    public void TheAnalysis_CoversThePullRequestAndTheDefaultBranch()
    {
        var text = SourceTree.ReadText(Workflow);

        new[]
        {
            ("pull_request:",
             "analyses no pull request, so a regression is found after it is merged"),
            ("branches: [master]",
             "analyses no push to master, so there is no baseline for a pull request to be compared against"),
            ("sonar.exclusions",
             "excludes nothing, so the generated client is measured like code somebody wrote — " +
             "two thousand machine-written lines deciding the duplication figure")
        }
            .Selected("setting the analysis depends on")
            .Where(setting => !text.Contains(setting.Item1, StringComparison.Ordinal))
            .Select(setting => $"'.github/workflows/sonar.yml' {setting.Item2}")
            .ShouldHold();
    }
}
