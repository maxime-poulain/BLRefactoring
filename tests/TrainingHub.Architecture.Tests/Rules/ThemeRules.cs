using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// How the visitor's theme is known before anything is painted, and what that costs (ADR 0077).
/// </summary>
/// <remarks>
/// The decision is cheap to describe and easy to undo by accident, which is why it is written down
/// twice — once as a record and once here. Two things can go: the resolver can drift back into C#,
/// where it runs after WebAssembly has booted and therefore after the prerendered page has already
/// been shown in the wrong palette; and the dark palette's two copies can stop agreeing, which is
/// not a build error and not a failing behavior test — it is a page that changes color a beat after
/// it appears.
/// </remarks>
public sealed partial class ThemeRules
{
    /// <summary>
    /// The page that resolves the theme, before any stylesheet has painted anything.
    /// </summary>
    private const string TheResolvingFile =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor/Components/App.razor";

    /// <summary>
    /// The layout that carries the palette C# knows.
    /// </summary>
    private const string TheLayout =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Layout/MainLayout.razor";

    /// <summary>
    /// The sheet that carries the palette the first paint uses.
    /// </summary>
    private const string TheSheet =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor/wwwroot/app.css";

    /// <summary>
    /// The variables a page cannot be painted without: the ground, the panels standing on it, and
    /// the ink on both.
    /// </summary>
    private static readonly string[] WhatTheFirstPaintNeeds =
        ["background", "surface", "text-primary"];

    /// <summary>
    /// The chosen theme, is resolved before the first paint.
    /// </summary>
    /// <remarks>
    /// Three halves that can fail separately. The first reads the resolving file: the head must
    /// still carry the script that reads the stored choice, falls back to the device's preference,
    /// and stamps the answer where a stylesheet can read it. The second refuses the old shape — a
    /// layout asking MudBlazor for the system preference is a layout deciding after the boot, which
    /// is the defect this record exists to close. The third compares the palette's two copies value
    /// by value, in both directions, because the sheet paints the prerendered page and C# repaints
    /// it a second later: where they disagree, the disagreement is visible.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0077",
        "the theme is resolved in the document's head, before the first paint, and the dark " +
        "palette's two copies say the same thing")]
    public void TheChosenTheme_IsResolvedBeforeTheFirstPaint()
    {
        static string Text(string relative) => SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        var head = Text(TheResolvingFile);
        var layout = Text(TheLayout);
        var sheet = Text(TheSheet);

        new[] { "data-theme", "localStorage.getItem(\"theme\")", "prefers-color-scheme" }
            .Selected("shape the resolver must keep")
            .Where(shape => !head.Contains(shape, StringComparison.Ordinal))
            .Select(shape =>
                $"'{TheResolvingFile}' no longer contains '{shape}'. The theme is resolved in the " +
                "head, from the stored choice and then from the device, before a stylesheet has " +
                "painted anything (ADR 0077)")
            .ShouldHold();

        new[] { "GetSystemDarkModeAsync" }
            .Selected("shape of a theme resolved after the boot")
            .Where(shape => layout.Contains(shape, StringComparison.Ordinal))
            .Select(shape =>
                $"'{TheLayout}' contains '{shape}'. Asking the provider for the system preference " +
                "answers after WebAssembly has booted, which is after the prerendered page has " +
                "been shown in the other palette (ADR 0077)")
            .ShouldHold();

        var written = PaletteDark(layout);
        var painted = DarkVariables(sheet);

        WhatTheFirstPaintNeeds
            .Selected("variable the first paint needs")
            .Where(variable => !painted.ContainsKey(variable))
            .Select(variable =>
                $"'{TheSheet}' declares no '--mud-palette-{variable}' under " +
                "':root[data-theme=\"dark\"]'. Without it the prerendered page is painted with the " +
                "light palette and corrected once the boot completes (ADR 0077)")
            .ShouldHold();

        written
            .Where(entry => painted.ContainsKey(entry.Key))
            .Selected("palette entry written in both copies")
            .Where(entry => !string.Equals(entry.Value, painted[entry.Key], StringComparison.Ordinal))
            .Select(entry =>
                $"the dark palette disagrees with itself on '{entry.Key}': the layout says " +
                $"'{entry.Value}' and the sheet says '{painted[entry.Key]}'. The sheet paints the " +
                "prerendered page and the layout repaints it a beat later, so a difference is a " +
                "flicker somebody will see (ADR 0077)")
            .ShouldHold();

        painted
            .Selected("variable the sheet paints the first frame with")
            .Where(entry => !written.ContainsKey(entry.Key))
            .Select(entry =>
                $"'{TheSheet}' declares '--mud-palette-{entry.Key}' under " +
                "':root[data-theme=\"dark\"]', and PaletteDark sets no twin for it. MudBlazor " +
                "writes its own value once the boot completes, so the first frame is painted with " +
                "a color the page then abandons (ADR 0077)")
            .ShouldHold();
    }

    /// <summary>
    /// The dark palette as C# writes it, keyed by the custom property each entry becomes.
    /// </summary>
    /// <remarks>
    /// The mapping is MudBlazor's, and it is regular but for one pair: a palette's
    /// <c>PrimaryContrastText</c> is written <c>--mud-palette-primary-text</c>, so the word
    /// <c>Contrast</c> disappears on the way out. Everything else is the property name cut at its
    /// capitals and joined with dashes.
    /// </remarks>
    private static Dictionary<string, string> PaletteDark(string layout)
    {
        var block = layout[layout.IndexOf("PaletteDark = new PaletteDark", StringComparison.Ordinal)..];

        return PaletteEntry()
            .Matches(block[..block.IndexOf("},", StringComparison.Ordinal)])
            .ToDictionary(
                entry => CustomProperty(entry.Groups["property"].Value),
                entry => entry.Groups["value"].Value);
    }

    /// <summary>
    /// The dark palette as the sheet declares it, keyed the same way.
    /// </summary>
    private static Dictionary<string, string> DarkVariables(string sheet)
    {
        var block = sheet[sheet.IndexOf(":root[data-theme=\"dark\"]", StringComparison.Ordinal)..];

        return Declaration()
            .Matches(block[..block.IndexOf('}', StringComparison.Ordinal)])
            .ToDictionary(
                declaration => declaration.Groups["variable"].Value,
                declaration => declaration.Groups["value"].Value.Trim());
    }

    private static string CustomProperty(string property) =>
        string.Concat(property.Replace("Contrast", string.Empty, StringComparison.Ordinal)
            .Select((character, index) => char.IsUpper(character) && index > 0
                ? $"-{char.ToLowerInvariant(character)}"
                : $"{char.ToLowerInvariant(character)}"));

    [GeneratedRegex(@"(?<property>\w+) = ""(?<value>[^""]+)""")]
    private static partial Regex PaletteEntry();

    [GeneratedRegex(@"--mud-palette-(?<variable>[a-z-]+):(?<value>[^;]+);")]
    private static partial Regex Declaration();
}
