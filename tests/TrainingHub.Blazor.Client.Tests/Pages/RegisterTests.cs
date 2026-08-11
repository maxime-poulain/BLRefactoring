using System.Text.Json;
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
/// Behavior covered for the account-creation page.
/// </summary>
/// <remarks>
/// The page registers through the BFF's own endpoint rather than the generated client: the
/// generated one points at <c>/api/Auth/register</c>, which the proxy answers with a 401 for
/// anyone without a session — and creating an account is the one thing a visitor with a session
/// never does. What the endpoint passes through is a problem document (ADR 0004) whose per-field
/// map carries the messages a user needs — "Email 'x' is already taken." — inside the
/// <c>errors</c> extension rather than in the title above it. NSwag models neither extension, so
/// both arrive as <see cref="JsonElement"/> and the reading is hand-written. That is the part
/// worth holding.
/// </remarks>
public sealed class RegisterTests : ComponentTest
{
    private readonly Mock<IBffSessionClient> _session = new();

    /// <summary>
    /// Register tests.
    /// </summary>
    public RegisterTests()
    {
        Services.AddSingleton(_session.Object);
    }

    /// <summary>
    /// Register, rejected with per-field messages, shows each of them.
    /// </summary>
    [Fact]
    public async Task Register_RejectedWithPerFieldMessages_ShowsEachOfThem()
    {
        // Arrange
        GivenRejection(Problem(
            title: "One or more validation errors occurred.",
            errors: """
                {"Email":["Email 'john@example.com' is already taken."],
                 "Password":["Passwords must have at least one digit."]}
                """));

        // Act
        await Register();

        // Assert
        Shown().Select(snackbar => snackbar.Message).Should().BeEquivalentTo(
            "Email 'john@example.com' is already taken.",
            "Passwords must have at least one digit.");
    }

    /// <summary>
    /// Register, rejected without a field map, falls back to the document's own words.
    /// </summary>
    /// <remarks>
    /// A domain refusal carries its codes under <c>domainErrors</c> and no <c>errors</c> map at
    /// all. Showing nothing in that case is the failure this covers: the page would report success
    /// by silence.
    /// </remarks>
    [Fact]
    public async Task Register_RejectedWithoutAFieldMap_FallsBackToTheDocumentsOwnWords()
    {
        // Arrange
        GivenRejection(Problem(title: "Bad Request", detail: "the contact address is already in use"));

        // Act
        await Register();

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("the contact address is already in use");
    }

    /// <summary>
    /// Register, rejected with an empty field map, still says something.
    /// </summary>
    [Fact]
    public async Task Register_RejectedWithAnEmptyFieldMap_StillSaysSomething()
    {
        // Arrange
        GivenRejection(Problem(title: "The registration was refused.", errors: "{}"));

        // Act
        await Register();

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The registration was refused.");
    }

    /// <summary>
    /// Register, the BFF was unreachable, reports an outage rather than a refusal.
    /// </summary>
    /// <remarks>
    /// The session client throws for a refusal it cannot read — a gateway's HTML page, an empty
    /// body. Rendering that as a validation verdict would send the user back to their form; what
    /// they need to hear is that the service, not their input, is the problem.
    /// </remarks>
    [Fact]
    public async Task Register_TheBffWasUnreachable_ReportsAnOutageRatherThanARefusal()
    {
        // Arrange
        _session
            .Setup(client => client.RegisterAsync(It.IsAny<RegisterHttpRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Registration answered 503 without a problem document."));

        // Act
        await Register();

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Registration is unavailable right now. Try again in a moment.");
    }

    /// <summary>
    /// Register, accepted, sends the visitor to sign in.
    /// </summary>
    [Fact]
    public async Task Register_Accepted_SendsTheVisitorToSignIn()
    {
        // Arrange
        _session
            .Setup(client => client.RegisterAsync(It.IsAny<RegisterHttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProblemDetails?)null);

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        // Act
        await Register();

        // Assert
        navigation.Uri.Should().Be("http://localhost/login");
        _session.Verify(client => client.RegisterAsync(It.Is<RegisterHttpRequest>(request =>
            request.Username == "john"
            && request.Email == "john@example.com"
            && request.Firstname == "John"
            && request.Lastname == "Doe"
            && request.Password == "Passw0rd!"
            && request.ConfirmPassword == "Passw0rd!"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void GivenRejection(ProblemDetails problem) =>
        _session
            .Setup(client => client.RegisterAsync(It.IsAny<RegisterHttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);

    private static ProblemDetails Problem(string title, string? detail = null, string? errors = null)
    {
        var problem = new ProblemDetails { Title = title, Detail = detail, Status = 400 };

        if (errors is not null)
        {
            // Deserialized rather than parsed from a JsonDocument: a document's RootElement stops
            // being readable the moment the document is disposed, and the page reads it later.
            problem.AdditionalProperties["errors"] = JsonSerializer.Deserialize<JsonElement>(errors);
        }

        return problem;
    }

    private async Task Register()
    {
        var page = Render<Register>();

        var fields = page.FindAll("input");
        fields[0].Input("John");
        fields[1].Input("Doe");
        fields[2].Input("john");
        fields[3].Input("john@example.com");
        fields[4].Input("Passw0rd!");
        fields[5].Input("Passw0rd!");

        // MudForm validates asynchronously, so the button is still disabled on the render that
        // follows the last keystroke, and bUnit drops an event aimed at a disabled element exactly
        // as a browser would. Waiting is what makes the click real rather than a no-op every
        // assertion below would then pass against.
        var register = () => page.FindAll("button")
            .Single(button => button.TextContent.Contains("Register", StringComparison.Ordinal));

        page.WaitForState(() => !register().HasAttribute("disabled"));

        await page.InvokeAsync(() => register().Click());
    }
}
