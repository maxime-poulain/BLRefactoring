using System.Security.Claims;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Layout;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Layout;

/// <summary>
/// Behavior covered for the application's frame: which doors show for whom, and how the corner
/// names its caller.
/// </summary>
/// <remarks>
/// The layout is the one component every page renders inside. The signed-in doors moved from a
/// row of buttons into the user menu (ADR 0074), so the facts about them open the menu first —
/// its content is not in the document until it is. The assertions read the anchors rather than
/// the words, because "TrainingHub" contains "Training" and a markup search would pass against
/// the brand alone.
/// </remarks>
public sealed class MainLayoutTests : ComponentTest
{
    /// <summary>
    /// Main layout tests.
    /// </summary>
    public MainLayoutTests()
    {
        Services.AddSingleton(Mock.Of<IBffSessionClient>());
        Services.AddSingleton(Mock.Of<IAuthenticationStateNotifier>());
    }

    /// <summary>
    /// Rendered signed out, offers the catalog and a sign-in, and none of the trainer's space.
    /// </summary>
    /// <remarks>
    /// The catalog's link sits outside the <c>AuthorizeView</c> on purpose: it is the one screen
    /// that does not ask who is looking, so it is offered whether or not anybody is signed in
    /// (ADR 0062).
    /// </remarks>
    [Fact]
    public void RenderedSignedOut_OffersTheCatalogAndASignIn_AndNoneOfTheTrainersSpace()
    {
        // Arrange
        this.AddAuthorization().SetNotAuthorized();

        // Act
        var layout = Render<MainLayout>();

        // Assert
        Links(layout).Should().Contain("/catalog").And.Contain("/login");
        Links(layout).Should().NotContain("/trainings").And.NotContain("/profile",
            "the trainer's space is behind the cookie, and its doors should not show to a visitor");
    }

    /// <summary>
    /// The open menu, offers the trainer's space, and no sign-in.
    /// </summary>
    [Fact]
    public void TheOpenMenu_OffersTheTrainersSpace_AndNoSignIn()
    {
        // Arrange
        this.AddAuthorization().SetAuthorized("alice");

        var layout = Render<MainLayout>();

        // Act
        layout.Find("button[aria-label='Account menu']").Click();

        // Assert
        layout.WaitForAssertion(() =>
            Links(layout).Should().Contain("/trainings").And.Contain("/profile"));
        Links(layout).Should().Contain("/catalog");
        Links(layout).Should().NotContain("/login",
            "a signed-in trainer is not invited to sign in again");
        Links(layout).Should().NotContain("/administration/trainers",
            "the administration's doors are the role's, not everybody's (ADR 0051)");
    }

    /// <summary>
    /// The open menu, offers an administrator the three administration doors too.
    /// </summary>
    /// <remarks>
    /// Hiding them from everyone else is courtesy, not security — the API refuses the calls
    /// behind <c>AdministratorPolicy</c> whatever the browser renders (ADR 0051, ADR 0054) — but
    /// an operator whose doors do not show has to type addresses from memory, which is its own
    /// kind of broken.
    /// </remarks>
    [Fact]
    public void TheOpenMenu_OffersAnAdministratorTheThreeAdministrationDoorsToo()
    {
        // Arrange
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("root");
        authorization.SetRoles("Administrator");

        var layout = Render<MainLayout>();

        // Act
        layout.Find("button[aria-label='Account menu']").Click();

        // Assert
        layout.WaitForAssertion(() =>
            Links(layout).Should().Contain("/administration/trainers")
                .And.Contain("/administration/trainings")
                .And.Contain("/administration/outbox"));
    }

    /// <summary>
    /// The menu, names the person the claims name.
    /// </summary>
    /// <remarks>
    /// The claims are the token's own (<c>firstname</c>, <c>lastname</c>), minted at sign-in for
    /// a trainer's account and carried through the cookie verbatim — so the name costs no call.
    /// </remarks>
    [Fact]
    public void TheMenu_NamesThePersonTheClaimsName()
    {
        // Arrange
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("alice");
        authorization.SetClaims(
            new Claim("firstname", "Alice"),
            new Claim("lastname", "Martin"));

        // Act
        var layout = Render<MainLayout>();

        // Assert
        Activator(layout).TextContent.Should().Contain("Alice Martin",
            "the corner identifies the signed-in person at a glance, by name");
    }

    /// <summary>
    /// The menu, falls back to the accounts name, when the token names no person.
    /// </summary>
    /// <remarks>
    /// An administrator's token carries no <c>firstname</c> and no <c>lastname</c> — an account
    /// is not a person with a profile — so the account's own name is what shows.
    /// </remarks>
    [Fact]
    public void TheMenu_FallsBackToTheAccountsName_WhenTheTokenNamesNoPerson()
    {
        // Arrange
        this.AddAuthorization().SetAuthorized("root");

        // Act
        var layout = Render<MainLayout>();

        // Assert
        Activator(layout).TextContent.Should().Contain("root");
        Activator(layout).TextContent.Should().Contain("R",
            "with no photo the avatar shows initials rather than a placeholder face");
    }

    /// <summary>
    /// The menu, shows the portrait, when there is one to show.
    /// </summary>
    /// <remarks>
    /// The address comes from the same one read the standing comes from, and it is the
    /// authenticated route with the photo's identity as a cache buster — never the public
    /// portrait route, which answers 404 for a suspended trainer (ADR 0063).
    /// </remarks>
    [Fact]
    public void TheMenu_ShowsThePortrait_WhenThereIsOneToShow()
    {
        // Arrange
        const string Address =
            "api/Trainer/9d1f7f2e-0e4a-4a1e-9f5f-2b3c4d5e6f70/photo?v=2b3c4d5e-6f70-4a1e-9f5f-9d1f7f2e0e4a";

        Portrait.Setup(source => source.FindOwnPortraitAsync()).ReturnsAsync(Address);
        this.AddAuthorization().SetAuthorized("alice");

        // Act
        var layout = Render<MainLayout>();

        // Assert
        layout.WaitForAssertion(() => layout.FindAll("img")
            .Should().Contain(image => image.GetAttribute("src") == Address));
    }

    /// <summary>
    /// Signing out from the menu, ends the session at the BFF.
    /// </summary>
    [Fact]
    public void SigningOutFromTheMenu_EndsTheSessionAtTheBff()
    {
        // Arrange
        var session = new Mock<IBffSessionClient>();
        Services.AddSingleton(session.Object);
        this.AddAuthorization().SetAuthorized("alice");

        var layout = Render<MainLayout>();
        layout.Find("button[aria-label='Account menu']").Click();

        // Act
        layout.WaitForAssertion(() => layout.FindAll(".mud-menu-item")
            .Should().Contain(item => item.TextContent.Contains("Sign out", StringComparison.Ordinal)));
        layout.FindAll(".mud-menu-item")
            .Single(item => item.TextContent.Contains("Sign out", StringComparison.Ordinal))
            .Click();

        // Assert
        layout.WaitForAssertion(() =>
            session.Verify(client => client.LogoutAsync(), Times.Once,
                "signing out is the BFF's business — it holds the only copy of the token (ADR 0009)"));
    }

    /// <summary>
    /// The brand, leads home.
    /// </summary>
    [Fact]
    public void TheBrand_LeadsHome()
    {
        // Arrange
        this.AddAuthorization().SetNotAuthorized();

        // Act
        var layout = Render<MainLayout>();

        // Assert
        layout.FindAll("a")
            .Single(anchor => anchor.TextContent.Trim() == "TrainingHub")
            .GetAttribute("href")
            .Should().Be("/", "the brand is the way home, and home is the catalog now (ADR 0074)");
    }

    /// <summary>
    /// The theme toggle, persists the choice.
    /// </summary>
    /// <remarks>
    /// The half that makes the toggle a preference rather than a mood: without the write, every
    /// visit started light again. The choice goes to the browser's own storage — a color is
    /// exactly what belongs in localStorage now that ADR 0009 took the credential out of it.
    /// </remarks>
    [Fact]
    public void TheThemeToggle_PersistsTheChoice()
    {
        // Arrange
        this.AddAuthorization().SetNotAuthorized();

        var layout = Render<MainLayout>();

        // Act
        layout.Find("button[aria-label='Toggle theme']").Click();

        // Assert
        layout.WaitForAssertion(() => JSInterop.Invocations
            .Should().Contain(invocation =>
                invocation.Identifier == "localStorage.setItem"
                && Equals(invocation.Arguments[0], "theme")
                && Equals(invocation.Arguments[1], "dark")));
    }

    /// <summary>
    /// A stored dark choice, is applied on the first render.
    /// </summary>
    /// <remarks>
    /// Proved through the toggle rather than through the palette: markup carries MudBlazor's
    /// generated styles, which are not this suite's to read. If the stored "dark" was applied,
    /// the next toggle stores "light" — and if it was not, this stores "dark" and fails.
    /// </remarks>
    [Fact]
    public void AStoredDarkChoice_IsAppliedOnTheFirstRender()
    {
        // Arrange
        this.AddAuthorization().SetNotAuthorized();
        JSInterop.Setup<string?>("localStorage.getItem", "theme").SetResult("dark");

        var layout = Render<MainLayout>();

        // Act
        layout.Find("button[aria-label='Toggle theme']").Click();

        // Assert
        layout.WaitForAssertion(() => JSInterop.Invocations
            .Should().Contain(invocation =>
                invocation.Identifier == "localStorage.setItem"
                && Equals(invocation.Arguments[1], "light")));
    }

    private static AngleSharp.Dom.IElement Activator(IRenderedComponent<MainLayout> layout) =>
        layout.Find("button[aria-label='Account menu']");

    private static IReadOnlyList<string?> Links(IRenderedComponent<MainLayout> layout) =>
        [.. layout.FindAll("a").Select(anchor => anchor.GetAttribute("href"))];
}
