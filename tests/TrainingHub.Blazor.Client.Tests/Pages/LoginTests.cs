using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using System.Security.Claims;
using TrainingHub.Blazor.Client.Authorization;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages;
using TrainingHub.Blazor.Client.Tests.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the sign-in page.
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
    private readonly BunitAuthorizationContext _caller;

    /// <summary>
    /// Login tests.
    /// </summary>
    /// <remarks>
    /// The identity is arranged rather than earned: the page reads it back from the provider after
    /// telling it the cookie changed, so what stands in for "what /bff/user now answers" is this
    /// context. Each fact says who signed in, because that is what decides where they land
    /// (ADR 0078).
    /// </remarks>
    public LoginTests()
    {
        _caller = this.AddAuthorization();
        Services.AddSingleton(_session.Object);
        Services.AddSingleton<IAuthenticationStateNotifier>(new BffAuthenticationStateProvider(
            new StubHttpClientFactory(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NoContent))));

        _session
            .Setup(client => client.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Sign in, a return address that is a path, is honored.
    /// </summary>
    [Fact]
    public async Task SignIn_AReturnAddressThatIsAPath_IsHonored()
    {
        // Arrange
        SignsInAsTrainer();

        // Act
        var navigation = await SignInWith("/trainings/create");

        // Assert
        navigation.Uri.Should().Be("http://localhost/trainings/create");
    }

    /// <summary>
    /// Sign in, a trainer with no return address, goes to their own trainings.
    /// </summary>
    [Fact]
    public async Task SignIn_ATrainerWithNoReturnAddress_GoesToTheirOwnTrainings()
    {
        // Arrange
        SignsInAsTrainer();

        // Act
        var navigation = await SignInWith(returnUrl: null);

        // Assert
        navigation.Uri.Should().Be("http://localhost/trainings");
    }

    /// <summary>
    /// Sign in, an administrator with no return address, goes to the administration.
    /// </summary>
    /// <remarks>
    /// The defect this replaces: every caller landed on the trainer's space, which an
    /// administrator's account has no standing on at all — the page's first act was a call the API
    /// answers 403, rendered as a red banner over an empty panel inviting them to create a training
    /// they cannot own (ADR 0051, ADR 0078).
    /// </remarks>
    [Fact]
    public async Task SignIn_AnAdministratorWithNoReturnAddress_GoesToTheAdministration()
    {
        // Arrange
        _caller.SetAuthorized("root");
        _caller.SetRoles(SessionRoles.Administrator);

        // Act
        var navigation = await SignInWith(returnUrl: null);

        // Assert
        navigation.Uri.Should().Be("http://localhost/administration");
    }

    /// <summary>
    /// Sign in, an account that is neither, goes to the catalog.
    /// </summary>
    /// <remarks>
    /// No such account exists today — registering creates a trainer, and the seeder creates the one
    /// administrator — but the code has to answer anyway, and the catalog is the one page that asks
    /// nothing of anybody (ADR 0074).
    /// </remarks>
    [Fact]
    public async Task SignIn_AnAccountThatIsNeither_GoesToTheCatalog()
    {
        // Arrange
        _caller.SetAuthorized("nobody");

        // Act
        var navigation = await SignInWith(returnUrl: null);

        // Assert
        navigation.Uri.Should().Be("http://localhost/");
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
        // Arrange
        SignsInAsTrainer();

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

    private void SignsInAsTrainer()
    {
        _caller.SetAuthorized("john");
        _caller.SetClaims(new Claim(SessionClaims.TrainerId, Guid.NewGuid().ToString()));
    }

    /// <summary>
    /// Sign in, while the credentials are in flight, says so and refuses a second submission.
    /// </summary>
    /// <remarks>
    /// This state stopped being cosmetic when Enter began submitting the form (ADR 0093). A button
    /// is pressed once; a key held down repeats. What keeps the second submission from reaching the
    /// session client is the same <c>disabled</c> the busy label announces, so the label and the
    /// attribute are asserted together — a spinner over a live button would be a lie about both.
    /// </remarks>
    [Fact]
    public async Task SignIn_WhileTheCredentialsAreInFlight_SaysSoAndRefusesASecondSubmission()
    {
        // Arrange
        var answering = new TaskCompletionSource<bool>();

        _session
            .Setup(client => client.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(answering.Task);

        var page = Render<Login>();

        var fields = page.FindAll("input");
        fields[0].Input("john");
        fields[1].Input("secret");

        // By either label, because the lookup must survive the flip to the busy one —
        // which is the very state this fact is about.
        var signIn = () => page.FindAll("button")
            .Single(button => button.TextContent.Contains("Sign in", StringComparison.Ordinal)
                || button.TextContent.Contains("Signing in", StringComparison.Ordinal));
        page.WaitForState(() => !signIn().HasAttribute("disabled"));

        // Act
        var submitted = page.InvokeAsync(() =>
            page.Find("form").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

        // Assert
        page.WaitForState(() => signIn().TextContent.Contains("Signing in", StringComparison.Ordinal));
        signIn().HasAttribute("disabled")
            .Should().BeTrue("a submission already on its way is what the second one has to wait for");

        answering.SetResult(true);
        await submitted;
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
            .Single(button => button.TextContent.Contains("Sign in", StringComparison.Ordinal));

        page.WaitForState(() => !signIn().HasAttribute("disabled"));

        // The gesture is Enter, dispatched where MudForm listens for it — its own form element.
        // The button's OnClick names the same handler, and the architecture rule holds that,
        // so one gesture exercises the one path both share (ADR 0093).
        await page.InvokeAsync(() => page.Find("form").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

        return navigation;
    }
}
