using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

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
public sealed partial class AnalysisRules
{
    private static string BuildProperties { get; } =
        Path.Combine(SourceTree.RepositoryRoot, "Directory.Build.props");

    private static string EditorConfig { get; } =
        Path.Combine(SourceTree.RepositoryRoot, ".editorconfig");

    /// <summary>
    /// The build, treats warnings as errors, and enforces code style.
    /// </summary>
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

    /// <summary>
    /// No diagnostic, is configured twice.
    /// </summary>
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

    /// <summary>
    /// Every demoted rule, says why it was demoted.
    /// </summary>
    /// <remarks>
    /// ADR 0019 wrote the principle — <em>every rule is enforced or demoted with its reason, there
    /// is no third category</em> — and named the two rules then in the second category. It did not
    /// notice that two others had been sitting in the third one all along: <c>CA1725</c> and
    /// <c>IDE0055</c> were suggestions with nothing beside them but their own title. The second of
    /// those is the diagnostic through which every formatting decision in that file is reported, so
    /// the brace style, the newline settings and the sorted <c>System</c> directives were preferences
    /// no build had ever checked. Neither was visible in a diff, and neither made anything red.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0020",
        "a rule set below warning carries the argument for lowering it, because a demotion nobody " +
        "wrote down is indistinguishable from one nobody made")]
    public void EveryDemotedRule_SaysWhyItWasDemoted()
    {
        var lines = SourceTree.ReadLines(EditorConfig);

        lines
            .Select((line, index) => (Line: line.Trim(), Number: index + 1))
            .Select(entry => (entry.Number, Match: Demotion.Match(entry.Line)))
            .Where(entry => entry.Match.Success)
            .Select(entry => (entry.Number, Rule: entry.Match.Groups["rule"].Value))
            .Selected("rule set below warning in .editorconfig")
            .Where(entry => ArgumentLinesAbove(lines, entry.Number - 1) < 2)
            .Select(entry =>
                $"'.editorconfig' lowers {entry.Rule} on line {entry.Number} without saying why. " +
                "A comment naming the rule is not a reason — the argument for turning a declared " +
                "rule off is the only thing that distinguishes a decision from an oversight, and " +
                "it is what ADR 0019 requires")
            .ShouldHold();
    }

    /// <summary>
    /// No setting, carries a trailing comment.
    /// </summary>
    /// <remarks>
    /// EditorConfig recognizes a comment only at the start of a line. Written after a value, the
    /// <c>#</c> and everything after it become part of the value, the parser fails to read it, and
    /// the setting silently falls back to its default — which is how <c>csharp_prefer_braces</c>
    /// went unread here for as long as it existed, and read correctly only because the default
    /// happened to agree with it. The header of that file warns about the same trap in its other
    /// form, where a <c>:severity</c> suffix on an option that takes none makes the line vanish.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0020",
        "a setting carries no trailing comment, because EditorConfig would read the comment as part " +
        "of the value and silently fall back to the default")]
    public void NoSetting_CarriesATrailingComment()
    {
        SourceTree.ReadLines(EditorConfig)
            .Select((line, index) => (Line: line.Trim(), Number: index + 1))
            .Where(entry => !entry.Line.StartsWith('#') && !entry.Line.StartsWith(';'))
            .Select(entry => (entry.Number, Match: Setting.Match(entry.Line)))
            .Where(entry => entry.Match.Success)
            .Selected("setting in .editorconfig")
            .Where(entry => entry.Match.Groups["value"].Value.IndexOfAny(['#', ';']) >= 0)
            .Select(entry =>
                $"'.editorconfig' line {entry.Number} ends in a comment. EditorConfig reads it as " +
                "part of the value, fails to parse the result and falls back to the default, so " +
                "the line goes on describing a setting that is not in force")
            .ShouldHold();
    }

    /// <summary>
    /// Counts the comment lines above a declaration, beyond the one that merely names the rule.
    /// </summary>
    /// <remarks>
    /// The walk steps over sibling declarations first: a family demoted together — the
    /// expression-bodied rules, the collection-expression ones — shares one comment block, and the
    /// line directly above the second of them is the first of them rather than any comment.
    /// </remarks>
    private static int ArgumentLinesAbove(IReadOnlyList<string> lines, int index)
    {
        var cursor = index - 1;

        while (cursor >= 0 && lines[cursor].Trim().Length > 0 && !IsComment(lines[cursor]))
        {
            cursor--;
        }

        var comments = 0;
        while (cursor >= 0 && IsComment(lines[cursor]))
        {
            comments++;
            cursor--;
        }

        return comments;
    }

    private static bool IsComment(string line) =>
        line.TrimStart().StartsWith('#') || line.TrimStart().StartsWith(';');

    [GeneratedRegex(@"^dotnet_diagnostic\.(?<rule>[A-Za-z]+[0-9]+)\.severity\s*=")]
    private static partial Regex Severity { get; }

    [GeneratedRegex(@"^dotnet_diagnostic\.(?<rule>[A-Za-z]+[0-9]+)\.severity\s*=\s*(suggestion|silent|none)\s*$")]
    private static partial Regex Demotion { get; }

    [GeneratedRegex(@"^[A-Za-z_][\w.]*\s*=\s*(?<value>.*)$")]
    private static partial Regex Setting { get; }
}
