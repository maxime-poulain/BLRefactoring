using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the forgot-password page.
/// </summary>
/// <remarks>
/// The page's one security property is a sentence: whatever the visitor types, a taken request is
/// answered with the same confirmation, because the page never learns — and so can never leak —
/// whether the address names an account (ADR 0084). The client's <see langword="bool"/> says only
/// "taken" or "throttled", and these facts hold the page to rendering nothing more specific.
/// </remarks>
public sealed class ForgotPasswordTests : ComponentTest
{
    private readonly Mock<IBffSessionClient> _session = new();

    /// <summary>
    /// Forgot password tests.
    /// </summary>
    public ForgotPasswordTests()
    {
        Services.AddSingleton(_session.Object);
    }

    /// <summary>
    /// Submit, any address, shows the one generic confirmation and retires the form.
    /// </summary>
    [Fact]
    public async Task Submit_AnyAddress_ShowsTheOneGenericConfirmation()
    {
        // Arrange
        _session
            .Setup(client => client.ForgotPasswordAsync("grace.hopper@example.org", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var page = await SubmitAsync("grace.hopper@example.org");

        // Assert
        page.Markup.Should().Contain("If an account exists for this address",
            "the confirmation is conditional on purpose — the page never says whether an account exists");
        page.FindAll("input").Should().BeEmpty("the confirmation replaces the form: there is nothing more to type");
        _session.Verify(client => client.ForgotPasswordAsync(
            "grace.hopper@example.org", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Submit, the window refused, says wait and keeps the form.
    /// </summary>
    [Fact]
    public async Task Submit_TheWindowRefused_SaysWaitAndKeepsTheForm()
    {
        // Arrange
        _session
            .Setup(client => client.ForgotPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var page = await SubmitAsync("grace.hopper@example.org");

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Too many requests. Wait a few minutes and try again.");
        page.FindAll("input").Should().NotBeEmpty(
            "a throttled request took nothing, so the form stays for the retry the sentence promises");
    }

    /// <summary>
    /// Submit, the BFF was unreachable, reports an outage rather than a confirmation.
    /// </summary>
    /// <remarks>
    /// The one lie this page must never tell: confirming "on its way" for a request that never
    /// arrived would strand the visitor at their inbox waiting for nothing.
    /// </remarks>
    [Fact]
    public async Task Submit_TheBffWasUnreachable_ReportsAnOutageRatherThanAConfirmation()
    {
        // Arrange
        _session
            .Setup(client => client.ForgotPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The BFF is down."));

        // Act
        var page = await SubmitAsync("grace.hopper@example.org");

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Password recovery is unavailable right now. Try again in a moment.");
        page.Markup.Should().NotContain("on its way");
    }

    private async Task<IRenderedComponent<ForgotPassword>> SubmitAsync(string email)
    {
        var page = Render<ForgotPassword>();

        page.FindAll("input")[0].Input(email);

        // MudForm validates asynchronously, so the button is still disabled on the render that
        // follows the keystroke — the register page's rule, for the register page's reason.
        var send = () => page.FindAll("button")
            .Single(button => button.TextContent.Contains("Send", StringComparison.Ordinal));

        page.WaitForState(() => !send().HasAttribute("disabled"));

        await page.InvokeAsync(() => send().Click());

        return page;
    }
}
