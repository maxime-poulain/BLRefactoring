using System.Text.RegularExpressions;
using System.Xml.Linq;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The words and the language: who may know the translations, and who resolves the culture.
/// </summary>
/// <remarks>
/// ADR 0088 draws one line and wires one pipeline. The line: the domain answers stable codes and
/// the boundary answers sentences, so the resource assembly is reachable from every surface and
/// referenced by no inner layer — and references nothing itself, so it can never become a corridor
/// between two layers the graph keeps apart. The pipeline: every host resolves the request's
/// culture the same way, and every culture file carries exactly the neutral file's keys, because
/// with English as the fallback a missing translation shows nothing at all — which is why the
/// drift has to be a red build rather than a bug report from a French visitor.
/// </remarks>
public sealed partial class LocalizationRules
{
    /// <summary>The suffix an inner-layer project's name carries, per stack and shared alike.</summary>
    private static readonly string[] InnerLayerSuffixes = [".Domain", ".Application", ".Infrastructure"];

    /// <summary>Where the resource families live, as the repository writes the path.</summary>
    private const string TranslationsRoot = "src/TrainingHub.Translations/";

    /// <summary>The composition roots the culture decision is about, by host.</summary>
    private static readonly string[] ApiHostPrograms =
    [
        Path.Combine("src", "DDD", "Api", "Program.cs"),
        Path.Combine("src", "DDDWithCqrs", "Api", "Program.cs"),
    ];

    /// <summary>
    /// No inner layer, references the translations.
    /// </summary>
    /// <remarks>
    /// The domain's half of this line is already held by <c>TheDomain_ReferencesTheKernelAnd
    /// NothingElse</c>; this rule is what keeps the other inner layers honest, because nothing
    /// else bounds what an application or infrastructure project may reference. Read from the
    /// project graph rather than compiled metadata, like every layering rule: the compiler prunes
    /// a reference no type uses yet, and the csproj is where the coupling decision is written.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "translations are presentation: the domain answers codes, the edge answers sentences, " +
        "and no inner layer references the resource assembly")]
    public void NoInnerLayer_ReferencesTheTranslations() =>
        ProjectGraph.Projects
            .Where(project => project.Name == "TrainingHub.Shared"
                              || InnerLayerSuffixes.Any(suffix =>
                                  project.Name.EndsWith(suffix, StringComparison.Ordinal)))
            .Selected("inner-layer project")
            .Where(project => project.ProjectReferences.Contains("TrainingHub.Translations"))
            .Select(project =>
                $"{project.RelativePath} references TrainingHub.Translations. A sentence's language " +
                "belongs to the caller and is known only at the boundary: an inner layer that could " +
                "read a resource would start answering in one, and the same decision would come out " +
                "worded by whoever happened to ask (ADR 0088)")
            .ShouldHold();

    /// <summary>
    /// The translations, depend on nothing.
    /// </summary>
    /// <remarks>
    /// The generated client's rule, for the generated client's reason: an assembly that every
    /// surface loads — the WebAssembly client included — carries words and nothing else, because
    /// anything it referenced would ride into all of them, and a reference from it to any layer
    /// would open a corridor between projects the graph keeps apart.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "the translations depend on nothing: one assembly every surface may load, carrying words alone")]
    public void TheTranslations_DependOnNothing()
    {
        var translations = ProjectGraph.Project("TrainingHub.Translations");

        translations.ProjectReferences
            .Select(reference => $"the translations reference the project '{reference}'")
            .Concat(translations.PackageReferences
                .Select(package => $"the translations reference the package '{package.Name}'"))
            .ShouldHold();
    }

    /// <summary>
    /// Every culture resource, carries exactly the defaults keys.
    /// </summary>
    /// <remarks>
    /// Exact equality rather than a subset in either direction, decided in ADR 0088: the
    /// framework's fallback means a missing key never shows raw on a screen, which is precisely
    /// why it would hide forever — an intentionally untranslated key is indistinguishable from a
    /// forgotten one, so in a showcase the silent drift is the defect. The set of languages a
    /// family must speak is read from <c>SupportedLanguages</c> itself, which also closes the
    /// other direction: a culture file for a language the list does not offer is dead weight
    /// nobody can select.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "every culture file carries exactly the neutral file's keys, for every supported language: " +
        "fallback hides a missing translation, so the drift must be a red build instead")]
    public void EveryCultureResource_CarriesExactlyTheDefaultsKeys()
    {
        var families = SourceTree.AllFiles
            .Where(path => path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Absolute: path, Relative: SourceTree.Relative(path)))
            .Where(file => file.Relative.StartsWith(TranslationsRoot, StringComparison.Ordinal))
            .GroupBy(file => FamilyOf(file.Relative))
            .Selected("resource family");

        var translated = DeclaredLanguages()
            .Where(language => !string.Equals(language, DeclaredDefault(), StringComparison.OrdinalIgnoreCase))
            .Selected("supported language besides the default");

        var violations = new List<string>();

        foreach (var family in families.OrderBy(family => family.Key, StringComparer.Ordinal))
        {
            var byCulture = family.ToDictionary(
                file => CultureOf(file.Relative),
                file => file,
                StringComparer.OrdinalIgnoreCase);

            if (!byCulture.TryGetValue(string.Empty, out var neutral))
            {
                violations.Add(
                    $"the family '{family.Key}' has no neutral file, so there is nothing to fall back to");
                continue;
            }

            var expected = Keys(neutral.Absolute);

            foreach (var language in translated)
            {
                if (!byCulture.TryGetValue(language, out var translation))
                {
                    violations.Add(
                        $"'{family.Key}' has no {language} file: a supported language with no " +
                        "translation would answer English out of a page in another language");
                    continue;
                }

                var keys = Keys(translation.Absolute);

                violations.AddRange(expected.Except(keys, StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .Select(key => $"{translation.Relative} misses the key '{key}'"));
                violations.AddRange(keys.Except(expected, StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .Select(key =>
                        $"{translation.Relative} carries the key '{key}', which the neutral file " +
                        "does not: a translation of nothing is a leftover"));
            }

            violations.AddRange(byCulture.Keys
                .Where(culture => culture.Length > 0
                                  && !translated.Contains(culture, StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(culture =>
                    $"'{family.Key}' carries a {culture} file, but SupportedLanguages does not " +
                    $"offer {culture}: a translation nobody can select is dead weight, and one " +
                    "that should be selectable belongs in the list first"));
        }

        violations.ShouldHold();
    }

    /// <summary>
    /// Both api hosts, resolve the same culture.
    /// </summary>
    /// <remarks>
    /// The logging rule's shape, for the logging rule's reason: ADR 0088 wires the culture
    /// resolution once in <c>TrainingHub.Shared.Api</c> precisely so neither host can quietly
    /// answer a language the other refuses, and that is a promise about two <c>Program.cs</c>
    /// files reflection cannot see. Comment lines are excluded before searching, so a call
    /// commented out stops counting as a call.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "the two hosts resolve the same culture: each Program.cs calls AddApiLocalization and UseApiLocalization")]
    public void BothApiHosts_ResolveTheSameCulture() =>
        ApiHostPrograms
            .Selected("API host Program.cs")
            .SelectMany(program =>
            {
                var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
                    .Split('\n')
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .ToArray();

                return new[] { "AddApiLocalization", "UseApiLocalization" }
                    .Where(call => !Array.Exists(code, line => line.Contains(call + "(", StringComparison.Ordinal)))
                    .Select(call =>
                        $"{program} never calls {call}. ADR 0088 resolves the culture once in " +
                        "Shared.Api precisely so neither host can answer a language the other " +
                        "refuses — add the call, or record the new decision");
            })
            .ShouldHold();

    /// <summary>The family a resource file belongs to: its name up to the culture, if any.</summary>
    private static string FamilyOf(string path)
    {
        var withoutFormat = Path.GetFileNameWithoutExtension(path);
        var culture = Path.GetExtension(withoutFormat);

        return culture.Length == 0 ? withoutFormat : Path.GetFileNameWithoutExtension(withoutFormat);
    }

    /// <summary>The culture segment of a resource file's name — <c>fr</c> — or empty for the neutral file.</summary>
    private static string CultureOf(string path)
    {
        var culture = Path.GetExtension(Path.GetFileNameWithoutExtension(path));

        return culture.Length == 0 ? string.Empty : culture[1..];
    }

    /// <summary>The keys a resource file declares, read from its XML rather than through a build.</summary>
    private static IReadOnlySet<string> Keys(string absolutePath) =>
        XDocument.Parse(SourceTree.ReadText(absolutePath))
            .Root!
            .Elements("data")
            .Select(data => (string?)data.Attribute("name"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"All\s*=\s*\[(?<items>[^\]]*)\]")]
    private static partial Regex DeclaredList { get; }

    [GeneratedRegex(@"Default\s*=\s*""(?<code>[a-zA-Z-]+)""")]
    private static partial Regex DeclaredDefaultCode { get; }

    [GeneratedRegex(@"""(?<code>[a-zA-Z-]+)""")]
    private static partial Regex QuotedCode { get; }

    /// <summary>The one list every surface shares, read from its source like the Program.cs pins.</summary>
    private static string SupportedLanguagesSource() =>
        SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot, "src", "TrainingHub.Translations", "SupportedLanguages.cs"));

    /// <summary>Every language <c>SupportedLanguages.All</c> declares.</summary>
    private static IReadOnlyList<string> DeclaredLanguages() =>
        [.. QuotedCode.Matches(DeclaredList.Match(SupportedLanguagesSource()).Groups["items"].Value)
            .Select(match => match.Groups["code"].Value)];

    /// <summary>The language <c>SupportedLanguages.Default</c> declares.</summary>
    private static string DeclaredDefault() =>
        DeclaredDefaultCode.Match(SupportedLanguagesSource()).Groups["code"].Value;
}
