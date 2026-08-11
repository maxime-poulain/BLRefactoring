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
    /// Renders, says nothing is here, and offers the anonymous ways out.
    /// </summary>
    [Fact]
    public void Renders_SaysNothingIsHere_AndOffersTheAnonymousWaysOut()
    {
        var page = Render<NotFound>();

        page.Markup.Should().Contain("There is nothing at this address");

        var links = page.FindAll("a").Select(anchor => anchor.GetAttribute("href")).ToList();

        links.Should().Contain("/catalog",
            "the catalog is the one place a lost visitor can always go (ADR 0062)");
        links.Should().Contain("/", "and home is the other");
    }
}
