using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Layout;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Layout;

/// <summary>
/// Behavior covered for the language selector: what it offers, and what picking one does.
/// </summary>
/// <remarks>
/// Rendered through the layout, like the user menu beside it: a menu opens through the popover
/// provider, and the layout is what renders one. The entries say each language's own name for
/// itself — the one label a visitor who cannot read the current language is guaranteed to
/// recognize — so the facts read those words rather than codes.
/// </remarks>
public sealed class CultureSelectorTests : ComponentTest
{
    /// <summary>
    /// Culture selector tests.
    /// </summary>
    public CultureSelectorTests()
    {
        // The layout renders the user menu beside the selector, and the menu injects the
        // session's services; inert doubles keep them out of these facts.
        Services.AddSingleton(Mock.Of<IBffSessionClient>());
        Services.AddSingleton(Mock.Of<IAuthenticationStateNotifier>());

        this.AddAuthorization().SetNotAuthorized();
    }

    /// <summary>
    /// The open menu, offers every language in its own words.
    /// </summary>
    [Fact]
    public void TheOpenMenu_OffersEveryLanguageInItsOwnWords()
    {
        var layout = Render<MainLayout>();

        layout.Find("button[aria-label='Language']").Click();

        layout.WaitForAssertion(() =>
        {
            var entries = layout.FindAll(".mud-menu-item").Select(item => item.TextContent.Trim());

            entries.Should().Contain(["English", "Français", "Русский"],
                "each entry names its language in that language, because the current one may be " +
                "unreadable to the person looking for theirs (ADR 0088)");
        });
    }

    /// <summary>
    /// Picking a language, records the choice and reboots the page in it.
    /// </summary>
    /// <remarks>
    /// The reload is the point: satellite assemblies and formatting data load when the runtime
    /// boots, so the honest way to speak the new language everywhere is to boot again — a
    /// re-render would translate the strings that happen to re-render and nothing else.
    /// </remarks>
    [Fact]
    public void PickingALanguage_RecordsTheChoiceAndRebootsThePageInIt()
    {
        CultureClient
            .Setup(client => client.ChangeLanguageAsync("fr", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var layout = Render<MainLayout>();

        PickFrench(layout);

        layout.WaitForAssertion(() =>
        {
            CultureClient.Verify(
                client => client.ChangeLanguageAsync("fr", It.IsAny<CancellationToken>()),
                Times.Once);
            navigation.History.Should().ContainSingle()
                .Which.Options.ForceLoad.Should().BeTrue(
                    "the runtime speaks the language it booted in, so the change is a full reload " +
                    "(ADR 0088)");
        });
    }

    /// <summary>
    /// The bff refusing, keeps the page and says so.
    /// </summary>
    [Fact]
    public void TheBffRefusing_KeepsThePageAndSaysSo()
    {
        CultureClient
            .Setup(client => client.ChangeLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var layout = Render<MainLayout>();

        PickFrench(layout);

        layout.WaitForAssertion(() =>
            Shown().Should().ContainSingle()
                .Which.Should().Match<Snackbar>(snackbar =>
                    snackbar.Message == "The language could not be changed. Try again in a moment."
                    && snackbar.Severity == Severity.Error));
        navigation.History.Should().BeEmpty(
            "a reload after a failed change would reboot the page in the language it already had, " +
            "and hide that nothing happened");
    }

    /// <summary>Opens the selector and picks French, the way a person does.</summary>
    private static void PickFrench(IRenderedComponent<MainLayout> layout)
    {
        layout.Find("button[aria-label='Language']").Click();
        layout.WaitForAssertion(() => layout.FindAll(".mud-menu-item")
            .Should().Contain(item => item.TextContent.Contains("Français", StringComparison.Ordinal)));
        layout.FindAll(".mud-menu-item")
            .Single(item => item.TextContent.Contains("Français", StringComparison.Ordinal))
            .Click();
    }
}
