using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages;
using TrainingHub.Blazor.Client.Tests.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behaviour covered for the sign-in page.
/// </summary>
/// <remarks>
/// The page's own logic is four lines long and one of them is a security control: where to go once
/// signed in is read from the address bar, so anything that could name another origin is discarded.
/// That check has no test anywhere else, it cannot be exercised from the API, and deleting it would
/// break nothing visible — a sign-in page that redirects wherever a query string says is a phishing
/// hop with this application's branding on it.
/// </remarks>
public sealed class LoginTests : ComponentTest
{
    private readonly Mock<IBffSessionClient> _session = new();

    /// <summary>
    /// Login tests.
    /// </summary>
    public LoginTests()
    {
        Services.AddSingleton(_session.Object);
        Services.AddSingleton(new BffAuthenticationStateProvider(
            new StubHttpClientFactory(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NoContent))));

        _session
            .Setup(client => client.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Sign in, a return address that is a path, is honoured.
    /// </summary>
    [Fact]
    public async Task SignIn_AReturnAddressThatIsAPath_IsHonoured()
    {
        // Act
        var navigation = await SignInWith("/trainings/create");

        // Assert
        navigation.Uri.Should().Be("http://localhost/trainings/create");
    }

    /// <summary>
    /// Sign in, no return address, goes to the catalogue.
    /// </summary>
    [Fact]
    public async Task SignIn_NoReturnAddress_GoesToTheCatalogue()
    {
        // Act
        var navigation = await SignInWith(returnUrl: null);

        // Assert
        navigation.Uri.Should().Be("http://localhost/trainings");
    }

    /// <summary>
    /// Sign in, a return address naming another origin, is refused.
    /// </summary>
    /// <remarks>
    /// The protocol-relative form is the one worth writing a test for.
    /// <c>Uri.IsWellFormedUriString("//evil.example", UriKind.Relative)</c> answers true, so the
    /// obvious guard passes it and the browser reads it as a host. The absolute form is here beside
    /// it because a check that catches only the surprising case tends to be rewritten by somebody
    /// who cannot see why it is not just <c>StartsWith('/')</c>.
    /// </remarks>
    [Theory]
    [InlineData("//evil.example")]
    [InlineData("//evil.example/trainings")]
    [InlineData("https://evil.example/trainings")]
    [InlineData("javascript:alert(1)")]
    public async Task SignIn_AReturnAddressNamingAnotherOrigin_IsRefused(string returnUrl)
    {
        // Act
        var navigation = await SignInWith(returnUrl);

        // Assert
        navigation.Uri.Should().Be("http://localhost/trainings");
    }

    /// <summary>
    /// Sign in, credentials refused, says so and stays put.
    /// </summary>
    [Fact]
    public async Task SignIn_CredentialsRefused_SaysSoAndStaysPut()
    {
        // Arrange
        _session
            .Setup(client => client.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var navigation = await SignInWith(returnUrl: null);

        // Assert
        Shown().Should().ContainSingle()
            .Which.Should().Match<Snackbar>(snackbar =>
                snackbar.Message == "Invalid username or password."
                && snackbar.Severity == Severity.Error);

        navigation.Uri.Should().Be("http://localhost/login");
    }

    /// <summary>
    /// Sign in, unavailable, does not put the exception on screen.
    /// </summary>
    /// <remarks>
    /// A <c>NullReferenceException</c> rendered as interface copy tells an attacker more than it
    /// tells the person reading it, and on a sign-in page the audience for that is not the user.
    /// </remarks>
    [Fact]
    public async Task SignIn_Unavailable_DoesNotPutTheExceptionOnScreen()
    {
        // Arrange
        _session
            .Setup(client => client.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Object reference not set to an instance of an object."));

        // Act
        await SignInWith(returnUrl: null);

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Sign-in is unavailable right now. Try again in a moment.");
    }

    /// <summary>
    /// Sign in, sends the credentials the visitor typed.
    /// </summary>
    [Fact]
    public async Task SignIn_SendsTheCredentialsTheVisitorTyped()
    {
        // Act
        await SignInWith(returnUrl: null);

        // Assert
        _session.Verify(
            client => client.LoginAsync("john", "secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task<BunitNavigationManager> SignInWith(string? returnUrl)
    {
        // Through the address bar, not as a parameter: `returnUrl` is bound with
        // [SupplyParameterFromQuery], and the whole point of the check under test is that the value
        // arrives from a place the visitor controls. bUnit refuses to set such a parameter directly
        // for exactly that reason.
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/login");

        if (returnUrl is not null)
        {
            navigation.NavigateTo(navigation.GetUriWithQueryParameter("returnUrl", returnUrl));
        }

        var page = Render<Login>();

        var fields = page.FindAll("input");
        fields[0].Input("john");
        fields[1].Input("secret");

        // MudForm validates asynchronously, so the button is still disabled on the render that
        // follows the last keystroke — and bUnit, like a browser, drops an event aimed at a
        // disabled element. Without this wait the click silently does nothing and every assertion
        // below passes for the wrong reason.
        var signIn = () => page.FindAll("button")
            .Single(button => button.TextContent.Contains("Sign In", StringComparison.Ordinal));

        page.WaitForState(() => !signIn().HasAttribute("disabled"));

        await page.InvokeAsync(() => signIn().Click());

        return navigation;
    }
}
