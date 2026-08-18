using System.Globalization;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using TrainingHub.Shared.Api.Extensions;
using TrainingHub.Translations;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Extensions;

/// <summary>
/// The annotations bridge: every template a house attribute declares is a key in the validation
/// family, and the provider both hosts wire resolves it there.
/// </summary>
/// <remarks>
/// The keys are the English templates themselves, so the lookup and the fallback answer the same
/// English sentence — which is what keeps every English-pinned suite indifferent to whether this
/// bridge exists (ADR 0089). These facts hold the two halves a host cannot see from its own
/// <c>Program.cs</c>: that the provider targets <see cref="ValidationResources"/>, and that a
/// French request actually gets the French template out of it.
/// </remarks>
public sealed class LocalizationExtensionsTests
{
    /// <summary>
    /// The wired provider, resolves a house template, in the request's language.
    /// </summary>
    [Fact]
    public void TheWiredProvider_ResolvesAHouseTemplate_InTheRequestsLanguage()
    {
        var localizer = WiredLocalizer();

        InCulture("fr", () =>
            localizer["The {0} field must name an identifier, and the empty one names none."].Value
                .Should().Be("Le champ {0} doit nommer un identifiant, et l'identifiant vide n'en nomme aucun.",
                    "the provider hands every annotation's template to the validation family, " +
                    "and the satellite carries the sentence"));
    }

    /// <summary>
    /// An English request, reads the template verbatim, hit or miss alike.
    /// </summary>
    /// <remarks>
    /// The invariant the whole bridge stands on: the neutral entry restates the template, and a
    /// template the family does not know falls back to itself — so English is byte-identical
    /// whether the lookup succeeded, failed, or never ran.
    /// </remarks>
    [Fact]
    public void AnEnglishRequest_ReadsTheTemplateVerbatim_HitOrMissAlike()
    {
        var localizer = WiredLocalizer();

        InCulture("en", () =>
        {
            localizer["The {0} field must name a status this API knows."].Value
                .Should().Be("The {0} field must name a status this API knows.");

            var unknown = localizer["The {0} field is one no attribute declares."];
            unknown.ResourceNotFound.Should().BeTrue();
            unknown.Value.Should().Be("The {0} field is one no attribute declares.",
                "the key is the sentence, so a miss and a hit read the same in English");
        });
    }

    /// <summary>
    /// The request localization, reads the accept language header, and nothing else.
    /// </summary>
    /// <remarks>
    /// The half of ADR 0088 a host cannot see from its own <c>Program.cs</c>: the culture comes
    /// from <c>Accept-Language</c> alone — no cookie, because the BFF owns the visitor's choice
    /// and says it in this header; no query string, because addresses stay culture-independent —
    /// and the list and the default are <see cref="SupportedLanguages"/>' own.
    /// </remarks>
    [Fact]
    public void TheRequestLocalization_ReadsTheAcceptLanguageHeader_AndNothingElse()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiLocalization();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IStringLocalizerFactory>().Should().NotBeNull(
            "the problem funnel resolves its catalog from this registration");

        var options = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

        options.DefaultRequestCulture.UICulture.Name.Should().Be(SupportedLanguages.Default);
        options.SupportedUICultures.Should().NotBeNull()
            .And.Subject.Select(culture => culture.Name)
            .Should().BeEquivalentTo(SupportedLanguages.All,
                "the three hosts and the WebAssembly boot share one list");
        options.RequestCultureProviders.Should().ContainSingle()
            .Which.Should().BeOfType<AcceptLanguageHeaderRequestCultureProvider>();
    }

    /// <summary>The localizer the wired provider builds, exactly as a host resolves it.</summary>
    private static IStringLocalizer WiredLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddControllers().AddApiValidationLocalization();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MvcDataAnnotationsLocalizationOptions>>();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        options.Value.DataAnnotationLocalizerProvider.Should().NotBeNull(
            "AddApiValidationLocalization is the one place the provider is set");

        return options.Value.DataAnnotationLocalizerProvider!(typeof(object), factory);
    }

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
