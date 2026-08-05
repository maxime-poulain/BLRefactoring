using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What a configuration knob owes the host that binds it.
/// </summary>
/// <remarks>
/// Three options classes learned this one at a time — storage, mail, logging — and the outbox
/// was the fourth, bound with no validation at all until ADR 0033. The rule generalises what the
/// precedents already agreed on, so the fifth section starts validated instead of rediscovering
/// the lesson. Measured, not assumed: written against the unvalidated outbox binding, and it
/// failed naming that line before the binding was converted.
/// </remarks>
public sealed partial class OptionsRules
{
    /// <summary>
    /// A binding site: <c>Configure&lt;TOptions&gt;</c> or <c>AddOptions&lt;TOptions&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Only statements that read a configuration section count as bindings — a
    /// <c>Configure&lt;TOptions&gt;</c> handing defaults to a lambda binds nothing and owes no
    /// validation, which is why the population is filtered on <c>GetSection</c> further down.
    /// </remarks>
    [GeneratedRegex(@"(?<method>\.Configure|AddOptions)<(?<options>\w+Options)>")]
    private static partial Regex BindingSite { get; }

    /// <summary>
    /// Every bound options, is validated at startup.
    /// </summary>
    [Fact]
    [ArchitectureRule("0033",
        "a knob that binds from configuration is validated at start-up, so a wrong value refuses " +
        "the host rather than surfacing as the worker's first failure")]
    public void EveryBoundOptions_IsValidatedAtStartup()
    {
        var sites = SourceTree.SourceFiles
            .Where(path => SourceTree.Relative(path).StartsWith("src/", StringComparison.Ordinal))
            .Where(path => !SourceTree.IsGenerated(path))
            .SelectMany(Bindings)
            .Where(site => site.Statement.Contains("GetSection(", StringComparison.Ordinal))
            .ToArray();

        sites
            .Selected("options binding site")
            .Where(site => !site.Statement.Contains(".ValidateOnStart()", StringComparison.Ordinal))
            .Select(site => site.Method == ".Configure"
                ? $"'{site.Path}' binds {site.Options} through Configure<>, which cannot validate " +
                  "at start-up. Bind through AddOptions<>().Bind(...).Validate(...).ValidateOnStart()"
                : $"'{site.Path}' binds {site.Options} without reaching ValidateOnStart(), so a " +
                  "wrong value waits for its first reader instead of refusing the host")
            .ShouldHold();
    }

    /// <summary>
    /// The binding statements of one file: each match with the text up to its terminating
    /// semicolon, which is as far as a fluent options chain can reach.
    /// </summary>
    private static IEnumerable<(string Path, string Method, string Options, string Statement)> Bindings(string path)
    {
        var text = SourceTree.ReadText(path);

        foreach (Match match in BindingSite.Matches(text))
        {
            var end = text.IndexOf(';', match.Index);
            var statement = end < 0 ? text[match.Index..] : text[match.Index..end];

            yield return (
                SourceTree.Relative(path),
                match.Groups["method"].Value,
                match.Groups["options"].Value,
                statement);
        }
    }
}
