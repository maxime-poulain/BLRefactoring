using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the reset-password page.
/// </summary>
/// <remarks>
/// The token arrives through the address bar and nothing else — the emailed link carries no
/// address and no identifier (ADR 0084) — so the email is typed into the form, and a page reached
/// without its token renders an explanation instead of a form that could only fail.
/// </remarks>
public sealed class ResetPasswordTests : ComponentTest
{
    private const string EmailedToken = "the-emailed-token";

    private readonly Mock<IBffSessionClient> _session = new();

    /// <summary>
    /// Reset password tests.
    /// </summary>
    public ResetPasswordTests()
    {
        Services.AddSingleton(_session.Object);
    }

    /// <summary>
    /// Rendered, without a token, offers a new link instead of a form.
    /// </summary>
    [Fact]
    public void Rendered_WithoutAToken_OffersANewLinkInsteadOfAForm()
    {
        // Act
        var page = Render<ResetPassword>();

        // Assert
        page.Markup.Should().Contain("missing its reset token",
            "a hand-typed or truncated address should be told what is wrong, not handed a form that can only fail");
        page.FindAll("input").Should().BeEmpty();
        page.FindAll("a").Should().Contain(anchor => anchor.GetAttribute("href") == "/forgot-password");
    }

    /// <summary>
    /// Submit, accepted, sends the visitor to sign in with exactly what they typed.
    /// </summary>
    [Fact]
    public async Task Submit_Accepted_SendsTheVisitorToSignIn()
    {
        // Arrange
        _session
            .Setup(client => client.ResetPasswordAsync(It.IsAny<ResetPasswordHttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProblemDetails?)null);

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        // Act
        await SubmitAsync();

        // Assert
        navigation.Uri.Should().Be("http://localhost/login");
        _session.Verify(client => client.ResetPasswordAsync(It.Is<ResetPasswordHttpRequest>(request =>
            request.Email == "grace.hopper@example.org"
            && request.Token == EmailedToken
            && request.Password == "a-new-password"
            && request.ConfirmPassword == "a-new-password"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Submit, rejected, shows the document's messages and stays on the page.
    /// </summary>
    [Fact]
    public async Task Submit_Rejected_ShowsTheDocumentsMessagesAndStays()
    {
        // Arrange
        _session
            .Setup(client => client.ResetPasswordAsync(It.IsAny<ResetPasswordHttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProblemDetails
            {
                Title = "One or more validation errors occurred.",
                Detail = "This password reset link is invalid or has expired. Request a new one and use the most recent email.",
                Status = 400
            });

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        // Act
        await SubmitAsync();

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Contain("invalid or has expired");
        navigation.Uri.Should().NotEndWith("/login", "a refused reset leaves the visitor where the retry is");
    }

    /// <summary>
    /// Submit, the BFF was unreachable, reports an outage rather than a refusal.
    /// </summary>
    [Fact]
    public async Task Submit_TheBffWasUnreachable_ReportsAnOutageRatherThanARefusal()
    {
        // Arrange
        _session
            .Setup(client => client.ResetPasswordAsync(It.IsAny<ResetPasswordHttpRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The password reset answered 503 without a problem document."));

        // Act
        await SubmitAsync();

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Password recovery is unavailable right now. Try again in a moment.");
    }

    private async Task SubmitAsync()
    {
        // Through the address bar, not as a parameter: the token is bound with
        // [SupplyParameterFromQuery], whose values come from the router's parsing of the URI, and
        // bUnit refuses to set such a parameter directly for exactly that reason.
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/reset-password");
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("token", EmailedToken));

        var page = Render<ResetPassword>();

        var fields = page.FindAll("input");
        fields[0].Input("grace.hopper@example.org");
        fields[1].Input("a-new-password");
        fields[2].Input("a-new-password");

        var change = () => page.FindAll("button")
            .Single(button => button.TextContent.Contains("Change the password", StringComparison.Ordinal));

        page.WaitForState(() => !change().HasAttribute("disabled"));

        await page.InvokeAsync(() => change().Click());
    }
}
