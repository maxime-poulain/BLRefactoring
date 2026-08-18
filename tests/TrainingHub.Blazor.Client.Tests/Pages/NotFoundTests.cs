using System.Globalization;
using System.Net;
using AwesomeAssertions;
using Bunit;
using TrainingHub.Blazor.Client.Pages;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the page an unknown address lands on.
/// </summary>
/// <remarks>
/// The router sends every unmatched address here rather than showing the blank screen it used to.
/// What is worth holding is the way out: a lost visitor is by definition somebody whose intent the
/// application does not know, so the doors offered are the anonymous ones — the catalog and home —
/// and never a page that would greet them with a sign-in redirect (ADR 0062).
/// </remarks>
public sealed class NotFoundTests : ComponentTest
{
    /// <summary>
    /// Renders, says nothing is here, and offers the anonymous way out.
    /// </summary>
    [Fact]
    public void Renders_SaysNothingIsHere_AndOffersTheAnonymousWayOut()
    {
        var page = Render<NotFound>();

        page.Markup.Should().Contain("There is nothing at this address");

        page.FindAll("a").Select(anchor => anchor.GetAttribute("href"))
            .Should().Contain("/",
                "the catalog is the one place a lost visitor can always go, and it is the front " +
                "door itself now (ADR 0062, ADR 0074)");
    }

    /// <summary>
    /// Renders, in the visitor's language, when the resolved culture is theirs.
    /// </summary>
    /// <remarks>
    /// One rendered proof that a screen's words follow the culture all the way into markup — the
    /// localizer, the satellite assembly, the component (ADR 0089). Every other surface goes
    /// through the same injection, which <c>TranslationsTests</c> holds per language at the
    /// lookup; this fact is the render the lookups cannot see. Decoded before asserting, because
    /// the renderer entity-encodes text it did not read from static markup.
    /// </remarks>
    [Fact]
    public void Renders_InTheVisitorsLanguage_WhenTheResolvedCultureIsTheirs()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");

        try
        {
            var page = Render<NotFound>();

            WebUtility.HtmlDecode(page.Markup)
                .Should().Contain("Il n'y a rien à cette adresse",
                    "the words on screen are the resolved culture's, not the neutral file's");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
