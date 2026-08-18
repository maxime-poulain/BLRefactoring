using System.Globalization;
using AwesomeAssertions;
using Bunit;
using Moq;
using MudBlazor;
using TrainingHub.Blazor.Client.Components;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Components;

/// <summary>
/// What the layout's verification banner shows, offers and absorbs (ADR 0090).
/// </summary>
/// <remarks>
/// The suspension banner's suite, one flag over, plus the two outcomes the resend button owns:
/// the 202 that becomes a success message, and the window's 429 that becomes a sentence rather
/// than an error — being told to wait is a verdict to render, since every resend also revokes
/// the pending link.
/// </remarks>
public sealed class VerificationBannerTests : ComponentTest
{
    /// <summary>
    /// The banner, an unverified account, asks for the click and offers the resend.
    /// </summary>
    [Fact]
    public void TheBanner_AnUnverifiedAccount_AsksForTheClickAndOffersTheResend()
    {
        // Arrange
        Standing.Setup(source => source.GetAsync())
            .ReturnsAsync(new TrainerStanding(IsSuspended: false, Reason: null, IsEmailVerified: false));

        // Act
        var banner = Render<VerificationBanner>();

        // Assert
        banner.Markup.Should().Contain("Verify your email address");
        banner.Markup.Should().Contain("Send the email again");
    }

    /// <summary>
    /// The banner, a verified account, renders nothing at all.
    /// </summary>
    /// <remarks>
    /// It sits above every routed page beside the suspension's, so an empty shell here would put
    /// a gap at the top of the application for everybody who already clicked.
    /// </remarks>
    [Fact]
    public void TheBanner_AVerifiedAccount_RendersNothingAtAll()
    {
        // Arrange — Active carries IsEmailVerified: true, the fail-open default.
        Standing.Setup(source => source.GetAsync()).ReturnsAsync(TrainerStanding.Active);

        // Act
        var banner = Render<VerificationBanner>();

        // Assert
        banner.Markup.Trim().Should().BeEmpty();
    }

    /// <summary>
    /// The resend, taken by the api, says the email is on its way.
    /// </summary>
    [Fact]
    public void TheResend_TakenByTheApi_SaysTheEmailIsOnItsWay()
    {
        // Arrange
        Standing.Setup(source => source.GetAsync())
            .ReturnsAsync(new TrainerStanding(IsSuspended: false, Reason: null, IsEmailVerified: false));
        AuthClient.Setup(client => client.ResendVerificationAsync()).Returns(Task.CompletedTask);

        var banner = Render<VerificationBanner>();

        // Act
        banner.Find("button").Click();

        // Assert
        AuthClient.Verify(client => client.ResendVerificationAsync(), Times.Once);
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Verification email sent. Only the most recent link works.");
    }

    /// <summary>
    /// The resend, refused by the window, asks the person to wait rather than reporting an error.
    /// </summary>
    [Fact]
    public void TheResend_RefusedByTheWindow_AsksThePersonToWait()
    {
        // Arrange
        Standing.Setup(source => source.GetAsync())
            .ReturnsAsync(new TrainerStanding(IsSuspended: false, Reason: null, IsEmailVerified: false));
        AuthClient.Setup(client => client.ResendVerificationAsync())
            .ThrowsAsync(new ApiException(
                "Too many requests", 429, null, new Dictionary<string, IEnumerable<string>>(), null));

        var banner = Render<VerificationBanner>();

        // Act
        banner.Find("button").Click();

        // Assert
        var shown = Shown().Should().ContainSingle().Subject;
        shown.Severity.Should().Be(Severity.Warning, "a window is a verdict, not an outage");
        shown.Message.Should().Contain("Wait a moment");
    }

    /// <summary>
    /// The banner, a french caller, asks in french.
    /// </summary>
    /// <remarks>
    /// One fact through the real localizer over the real satellite assembly, because the neutral
    /// facts above would stay green if the French file lost these keys' meaning — key parity is a
    /// rule, translation fidelity is spot-checked here (ADR 0088).
    /// </remarks>
    [Fact]
    public void TheBanner_AFrenchCaller_AsksInFrench()
    {
        // Arrange
        Standing.Setup(source => source.GetAsync())
            .ReturnsAsync(new TrainerStanding(IsSuspended: false, Reason: null, IsEmailVerified: false));

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");

        try
        {
            // Act
            var banner = Render<VerificationBanner>();

            // Assert
            banner.Markup.Should().Contain("Vérifiez votre adresse e-mail");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
