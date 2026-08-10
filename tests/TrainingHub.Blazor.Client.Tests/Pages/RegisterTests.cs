using System.Text.Json;
using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Pages;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the account-creation page.
/// </summary>
/// <remarks>
/// This page is the only one in the application that reads a problem document's per-field map. The
/// API used to answer registration with a bare array of identity errors and now answers RFC 7807
/// like everything else (ADR 0004), so the messages a user needs — "Email 'x' is already taken." —
/// are inside the <c>errors</c> extension rather than in the title above it. NSwag models neither
/// extension, so both arrive as <see cref="JsonElement"/> and the reading is hand-written. That is
/// the part worth holding.
/// </remarks>
public sealed class RegisterTests : ComponentTest
{
    private readonly Mock<IAuthClient> _auth = new();

    /// <summary>
    /// Register tests.
    /// </summary>
    public RegisterTests()
    {
        Services.AddSingleton(_auth.Object);
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
    /// Register, the API was unreachable, does not show the generator's own sentence.
    /// </summary>
    /// <remarks>
    /// "The HTTP status code of the response was not expected (503)" is NSwag describing its own
    /// disappointment. It belongs in the console.
    /// </remarks>
    [Fact]
    public async Task Register_TheApiWasUnreachable_DoesNotShowTheGeneratorsOwnSentence()
    {
        // Arrange
        _auth
            .Setup(client => client.RegisterAsync(It.IsAny<RegisterHttpRequest>()))
            .ThrowsAsync(new ApiException(
                "The HTTP status code of the response was not expected (503).",
                503,
                response: null,
                new Dictionary<string, IEnumerable<string>>(),
                null));

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
        _auth
            .Setup(client => client.RegisterAsync(It.IsAny<RegisterHttpRequest>()))
            .ReturnsAsync(Guid.NewGuid());

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        // Act
        await Register();

        // Assert
        navigation.Uri.Should().Be("http://localhost/login");
        _auth.Verify(client => client.RegisterAsync(It.Is<RegisterHttpRequest>(request =>
            request.Username == "john"
            && request.Email == "john@example.com"
            && request.Firstname == "John"
            && request.Lastname == "Doe"
            && request.Password == "Passw0rd!"
            && request.ConfirmPassword == "Passw0rd!")), Times.Once);
    }

    private void GivenRejection(ProblemDetails problem) =>
        _auth
            .Setup(client => client.RegisterAsync(It.IsAny<RegisterHttpRequest>()))
            .ThrowsAsync(new ApiException<ProblemDetails>(
                "Bad Request", 400, response: null, new Dictionary<string, IEnumerable<string>>(), problem, null));

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
