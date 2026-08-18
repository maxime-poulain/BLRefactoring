using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// The verification page's three states, and the one thing it must never do: spend the token
/// without a click (ADR 0090).
/// </summary>
public sealed class VerifyEmailTests : ComponentTest
{
    private readonly Mock<IBffSessionClient> _sessionClient = new();

    /// <summary>Verify email tests.</summary>
    public VerifyEmailTests()
    {
        Services.AddSingleton(_sessionClient.Object);
    }

    /// <summary>
    /// The page, reached without a token, says there is nothing to confirm.
    /// </summary>
    [Fact]
    public void ThePage_ReachedWithoutAToken_SaysThereIsNothingToConfirm()
    {
        // Act
        var page = Render<VerifyEmail>();

        // Assert
        page.Markup.Should().Contain("missing its verification token");
        page.FindAll("button").Should().BeEmpty("with nothing to redeem there is nothing to offer");
    }

    /// <summary>
    /// The page, reached with a token, redeems nothing until the button is pressed.
    /// </summary>
    /// <remarks>
    /// The decision this page exists to hold: inbox scanners open every address an email
    /// carries, and a redemption on load would let a machine spend the credential before its
    /// owner ever saw the page (ADR 0090).
    /// </remarks>
    [Fact]
    public void ThePage_ReachedWithAToken_RedeemsNothingUntilTheButtonIsPressed()
    {
        // Act
        var page = RenderWithToken();

        // Assert
        page.Markup.Should().Contain("Confirm my address");
        _sessionClient.Verify(
            client => client.VerifyEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "rendering the page must not spend the credential — only the click may");
    }

    /// <summary>
    /// The click, a live link, proves the address and refreshes the standing.
    /// </summary>
    /// <remarks>
    /// The refresh is what kills the banner without a logout: the standing is read from the
    /// database on every refresh, never from the token's claims (ADR 0090).
    /// </remarks>
    [Fact]
    public void TheClick_ALiveLink_ProvesTheAddressAndRefreshesTheStanding()
    {
        // Arrange
        _sessionClient
            .Setup(client => client.VerifyEmailAsync("the-emailed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Standing.Setup(source => source.RefreshAsync()).ReturnsAsync(TrainerStanding.Active);

        var page = RenderWithToken();

        // Act
        page.Find("button").Click();

        // Assert
        page.Markup.Should().Contain("Your email address is verified");
        Standing.Verify(source => source.RefreshAsync(), Times.Once);
    }

    /// <summary>
    /// The click, a dead link, renders the one sentence and keeps the button.
    /// </summary>
    /// <remarks>
    /// One sentence whatever killed the link, rendered by the page because the BFF passes only
    /// the status through — and its second half exists because "already used" is usually the
    /// happy path's own echo: the visitor who double-clicked has nothing to fix but signing in
    /// (ADR 0090).
    /// </remarks>
    [Fact]
    public void TheClick_ADeadLink_RendersTheOneSentenceAndKeepsTheButton()
    {
        // Arrange
        _sessionClient
            .Setup(client => client.VerifyEmailAsync("the-emailed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var page = RenderWithToken();

        // Act
        page.Find("button").Click();

        // Assert
        page.Markup.Should().Contain("invalid or has already been used");
        page.Markup.Should().Contain("just sign in");
        Standing.Verify(source => source.RefreshAsync(), Times.Never,
            "nothing was proven, so there is nothing to refresh");
    }

    /// <summary>Arrives the way the emailed link arrives: the token in the page's own query.</summary>
    private IRenderedComponent<VerifyEmail> RenderWithToken()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("token", "the-emailed-token"));

        return Render<VerifyEmail>();
    }
}
