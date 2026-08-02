using System.Text.RegularExpressions;
using BLRefactoring.Architecture.Tests.Framework;
using Xunit;

namespace BLRefactoring.Architecture.Tests.Rules;

/// <summary>
/// The analyzer configuration says what it does.
/// </summary>
/// <remarks>
/// Both rules here guard a failure that is <em>green</em>, which is the only kind worth a test in
/// this file. A ruleset nothing enforces produces a passing build and a false README; a rule
/// declared twice produces a passing build and a severity nobody chose. Neither is visible in a
/// diff, and neither can be found by reading the code the rules are supposed to govern.
/// <para>
/// This file exists because the repository was in both states at once. <c>.editorconfig</c> set
/// seventy-three rules to <c>warning</c> while no project set <c>TreatWarningsAsErrors</c> and no
/// workflow passed <c>-warnaserror</c>, so every one of them was emitted and ignored — ADR 0017 said
/// so in writing and left it. And <c>CA2016</c> was declared twice in the same section, seventy-eight
/// lines apart, so the rule that forwards a <c>CancellationToken</c> — the subject of a whole batch
/// of work here — was silently a suggestion.
/// </para>
/// </remarks>
public sealed class AnalysisRules
{
    private static string BuildProperties { get; } =
        Path.Combine(SourceTree.RepositoryRoot, "Directory.Build.props");

    private static string EditorConfig { get; } =
        Path.Combine(SourceTree.RepositoryRoot, ".editorconfig");

    [Fact]
    [ArchitectureRule("0019",
        "the build fails on a warning, so a severity written in .editorconfig is a rule rather than a preference")]
    public void TheBuild_TreatsWarningsAsErrors_AndEnforcesCodeStyle()
    {
        var text = SourceTree.ReadText(BuildProperties);

        new[]
        {
            (Property: "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
             Wrong: "does not turn warnings into errors, so every severity in .editorconfig is a " +
                    "preference an editor may show and a build will ignore — which is the state " +
                    "ADR 0017 recorded and declined to fix"),
            (Property: "<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>",
             Wrong: "does not enforce code style at build time, so the IDE* rules in .editorconfig " +
                    "run in an editor and nowhere else. They are the half of the ruleset that was " +
                    "never running at all")
        }
            .Selected("property the analyzer configuration depends on")
            .Where(rule => !text.Contains(rule.Property, StringComparison.Ordinal))
            .Select(rule => $"'Directory.Build.props' {rule.Wrong}")
            .ShouldHold();
    }

    [Fact]
    [ArchitectureRule("0019",
        "a diagnostic is configured once, because the second declaration wins in silence")]
    public void NoDiagnostic_IsConfiguredTwice()
    {
        // Severities are matched across the whole file rather than per section. EditorConfig
        // resolves the last matching key, so two sections whose globs both cover a file behave the
        // same way as two lines in one section: the later wins, and the earlier goes on reading as
        // though it were in force.
        var declarations = SourceTree.ReadLines(EditorConfig)
            .Select((line, index) => (Line: line.Trim(), Number: index + 1))
            .Select(entry => (entry.Number, Match: Severity.Match(entry.Line)))
            .Where(entry => entry.Match.Success)
            .Select(entry => (entry.Number, Rule: entry.Match.Groups["rule"].Value))
            .ToList();

        declarations
            .GroupBy(declaration => declaration.Rule, StringComparer.OrdinalIgnoreCase)
            .Selected("diagnostic configured in .editorconfig")
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"'.editorconfig' configures {group.Key} on lines " +
                $"{string.Join(", ", group.Select(declaration => declaration.Number))}. The last one " +
                "wins and the others read as though they were in force, so a rule can be demoted by " +
                "a duplicate nobody notices — which is how CA2016 became a suggestion")
            .ShouldHold();
    }

    private static readonly Regex Severity =
        new(@"^dotnet_diagnostic\.(?<rule>[A-Za-z]+[0-9]+)\.severity\s*=", RegexOptions.Compiled);
}
