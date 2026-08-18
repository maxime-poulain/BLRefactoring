using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TrainingHub.Translations;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests;

/// <summary>
/// The resource families answer, in every supported language.
/// </summary>
/// <remarks>
/// Through the real factory over the real satellite assemblies, because the mechanism is the
/// decision (ADR 0088): a key that resolves here resolves the same way on every surface that
/// calls <c>AddLocalization</c>. The culture is set per fact and restored in the same fact, so
/// nothing leaks into a suite that expects the runner's own.
/// </remarks>
public sealed class TranslationsTests
{
    // Logging first, because the localizer factory takes a logger: the one line a surface's own
    // host brings for free and a bare collection does not.
    private static readonly ServiceProvider Provider =
        new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();

    /// <summary>
    /// A common key, answers in the asked language.
    /// </summary>
    /// <param name="language">The language to resolve in.</param>
    /// <param name="expected">The word that language's resource carries.</param>
    [Theory]
    [InlineData("en", "Language")]
    [InlineData("fr", "Langue")]
    [InlineData("ru", "Язык")]
    public void ACommonKey_AnswersInTheAskedLanguage(string language, string expected) =>
        InCulture(language, () =>
            Localizer<CommonResources>()["Language"].Value.Should().Be(expected,
                "the satellite for {0} carries the word, and the factory finds it by the marker " +
                "type alone (ADR 0088)", language));

    /// <summary>
    /// A domain error, answers by the domains own code.
    /// </summary>
    /// <remarks>
    /// The family's keys are the very codes the aggregates raise — the domain stays code-only and
    /// translation-free, and the edge looks the visitor's sentence up here (ADR 0088). The
    /// neutral entry restates the domain's English verbatim, so an English answer reads the same
    /// whichever side authored it.
    /// </remarks>
    [Fact]
    public void ADomainError_AnswersByTheDomainsOwnCode() =>
        InCulture("en", () =>
            Localizer<DomainErrorResources>()["Trainer.AlreadySuspended"].Value.Should().Be(
                "This trainer is already suspended.",
                "the key is the domain's stable code, and the neutral sentence is the domain's own"));

    /// <summary>
    /// A key nobody wrote, answers itself and says so.
    /// </summary>
    [Fact]
    public void AKeyNobodyWrote_AnswersItselfAndSaysSo()
    {
        var answer = Localizer<CommonResources>()["Nonexistent"];

        answer.ResourceNotFound.Should().BeTrue("the localizer never throws; it reports instead");
        answer.Value.Should().Be("Nonexistent",
            "the key itself is the honest fallback a screen would show — which is why the " +
            "architecture rule holds every culture to exactly the neutral file's keys");
    }

    /// <summary>The localizer a surface would inject, resolved the way a surface resolves it.</summary>
    private static IStringLocalizer<TResources> Localizer<TResources>() =>
        Provider.GetRequiredService<IStringLocalizer<TResources>>();

    /// <summary>Runs one assertion under a culture, and restores the runner's own whatever happens.</summary>
    private static void InCulture(string language, Action assertion)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);

        try
        {
            assertion();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
