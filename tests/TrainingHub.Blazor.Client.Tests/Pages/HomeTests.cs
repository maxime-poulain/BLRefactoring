using AwesomeAssertions;
using Bunit;
using TrainingHub.Blazor.Client.Pages;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the landing page.
/// </summary>
/// <remarks>
/// The page is two panels behind one <c>AuthorizeView</c>, and the anonymous half is the one with a
/// history: it used to say that everything past this page required an account, which stopped being
/// true the day the catalog got a face (ADR 0062). Rendering both branches is what keeps that
/// sentence from creeping back — until here, the route at <c>/</c> was the one page no test had
/// ever rendered.
/// </remarks>
public sealed class HomeTests : ComponentTest
{
    /// <summary>
    /// Rendered signed out, offers the catalog first and an account for publishing.
    /// </summary>
    /// <remarks>
    /// Two panels in that order, and the order is the message: looking needs nobody's permission,
    /// an account is what publishing is for.
    /// </remarks>
    [Fact]
    public void RenderedSignedOut_OffersTheCatalogFirst_AndAnAccountForPublishing()
    {
        // Arrange
        this.AddAuthorization().SetNotAuthorized();

        // Act
        var page = Render<Home>();

        // Assert
        page.Markup.Should().Contain("Browse the catalog",
            "the anonymous panel leads with what needs no account (ADR 0062)");
        page.Markup.Should().Contain("No account needed");

        Link(page, "Browse trainings").Should().Be("/catalog");
        Link(page, "Sign in").Should().Be("/login");
        Link(page, "Create an account").Should().Be("/register");

        page.Markup.Should().NotContain("Signed in as",
            "the signed-in panel has no business greeting a visitor");
    }

    /// <summary>
    /// Rendered signed in, greets by name and leads to the trainings.
    /// </summary>
    [Fact]
    public void RenderedSignedIn_GreetsByName_AndLeadsToTheTrainings()
    {
        // Arrange
        this.AddAuthorization().SetAuthorized("alice");

        // Act
        var page = Render<Home>();

        // Assert
        page.Markup.Should().Contain("Signed in as alice",
            "the name comes from the cookie's own claims, and a blank greeting is how a broken " +
            "claim mapping would look");
        Link(page, "Go to trainings").Should().Be("/trainings");
        Link(page, "Browse the catalog").Should().Be("/catalog",
            "a trainer is a visitor too: the catalog shows them their own trainings the way " +
            "everyone else sees them (ADR 0062)");

        page.Markup.Should().NotContain("Sign in to publish",
            "a signed-in trainer is not invited to sign in again");
    }

    private static string? Link(IRenderedComponent<Home> page, string text) =>
        page.FindAll("a").Single(anchor => anchor.TextContent.Trim() == text).GetAttribute("href");
}
