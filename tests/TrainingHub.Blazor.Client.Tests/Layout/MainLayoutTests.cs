using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Layout;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Layout;

/// <summary>
/// Behavior covered for the application's frame: which doors show for whom.
/// </summary>
/// <remarks>
/// The layout is the one component every page renders inside, and until here no test had ever
/// rendered it — the navigation it offers an anonymous visitor, a signed-in trainer and an
/// administrator was three claims with nothing behind them. The assertions read the anchors
/// rather than the words, because "TrainingHub" contains "Training" and a markup search would
/// pass against the brand alone.
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
    /// Rendered signed in, offers the trainer's space, and no sign-in.
    /// </summary>
    [Fact]
    public void RenderedSignedIn_OffersTheTrainersSpace_AndNoSignIn()
    {
        // Arrange
        this.AddAuthorization().SetAuthorized("alice");

        // Act
        var layout = Render<MainLayout>();

        // Assert
        Links(layout).Should().Contain("/trainings").And.Contain("/profile").And.Contain("/catalog");
        Links(layout).Should().NotContain("/login",
            "a signed-in trainer is not invited to sign in again");
        Links(layout).Should().NotContain("/administration/trainers",
            "the administration's doors are the role's, not everybody's (ADR 0051)");
    }

    /// <summary>
    /// Rendered as an administrator, offers the three administration doors too.
    /// </summary>
    /// <remarks>
    /// Hiding them from everyone else is courtesy, not security — the API refuses the calls
    /// behind <c>AdministratorPolicy</c> whatever the browser renders (ADR 0051, ADR 0054) — but
    /// an operator whose doors do not show has to type addresses from memory, which is its own
    /// kind of broken.
    /// </remarks>
    [Fact]
    public void RenderedAsAnAdministrator_OffersTheThreeAdministrationDoorsToo()
    {
        // Arrange
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("root");
        authorization.SetRoles("Administrator");

        // Act
        var layout = Render<MainLayout>();

        // Assert
        Links(layout).Should().Contain("/administration/trainers")
            .And.Contain("/administration/trainings")
            .And.Contain("/administration/outbox");
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
            .Should().Be("/", "the brand is the way home, as on every site a visitor has ever used");
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

    private static IReadOnlyList<string?> Links(IRenderedComponent<MainLayout> layout) =>
        [.. layout.FindAll("a").Select(anchor => anchor.GetAttribute("href"))];
}
