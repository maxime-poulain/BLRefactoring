using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TrainingHub.Blazor.Client.Pages.Trainings;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behaviour covered for the catalogue of a trainer's own trainings.
/// </summary>
/// <remarks>
/// The page used to load every training in the system and hide the buttons on the ones belonging to
/// somebody else, which is not filtering: the rows had already reached the browser and anyone could
/// read them from the network tab. It now asks the server for its own, and that is the assertion
/// worth keeping — the rest is what a person sees when the answer is empty, or a refusal.
/// </remarks>
public sealed class TrainingsTests : ComponentTest
{
    private readonly Mock<ITrainingClient> _trainings = new();

    /// <summary>
    /// Trainings tests.
    /// </summary>
    public TrainingsTests()
    {
        Services.AddSingleton(_trainings.Object);

        _trainings.Setup(client => client.GetMineAsync()).ReturnsAsync([]);
    }

    /// <summary>
    /// Renders, nothing published yet, offers to create the first.
    /// </summary>
    [Fact]
    public void Renders_NothingPublishedYet_OffersToCreateTheFirst()
    {
        // Act
        var page = Render<Trainings>();

        // Assert
        page.Markup.Should().Contain("No trainings yet").And.Contain("Create Training");
    }

    /// <summary>
    /// Renders, trainings published, shows each with its topics.
    /// </summary>
    [Fact]
    public void Renders_TrainingsPublished_ShowsEachWithItsTopics()
    {
        // Arrange
        _trainings.Setup(client => client.GetMineAsync()).ReturnsAsync(
        [
            Training("Domain-Driven Design", "Programming", "Business"),
            Training("Public speaking", "Leadership")
        ]);

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Markup.Should().NotContain("No trainings yet");
        page.Markup.Should()
            .Contain("Domain-Driven Design").And.Contain("Public speaking")
            .And.Contain("Programming").And.Contain("Business").And.Contain("Leadership");
    }

    /// <summary>
    /// Renders, asks the server for its own trainings rather than for all of them.
    /// </summary>
    /// <remarks>
    /// The whole reason this page changed. Reading the trainer separately and comparing owners in
    /// the markup is what it replaced, and nothing about the rendered page would look different if
    /// it came back.
    /// </remarks>
    [Fact]
    public void Renders_AsksTheServerForItsOwnTrainings()
    {
        // Act
        Render<Trainings>();

        // Assert
        _trainings.Verify(client => client.GetMineAsync(), Times.Once);
    }

    /// <summary>
    /// Renders, the request was refused, shows the document's own words.
    /// </summary>
    [Fact]
    public void Renders_TheRequestWasRefused_ShowsTheDocumentsOwnWords()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync())
            .ThrowsAsync(new ApiException<ProblemDetails>(
                "Bad Request",
                400,
                response: null,
                new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Title = "Bad Request", Detail = "the page number must be positive" },
                null));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Message.Should().Be("the page number must be positive"));
    }

    /// <summary>
    /// Renders, the API was unreachable, does not show the generator's own sentence.
    /// </summary>
    [Fact]
    public void Renders_TheApiWasUnreachable_DoesNotShowTheGeneratorsOwnSentence()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync())
            .ThrowsAsync(new ApiException(
                "The HTTP status code of the response was not expected (503).",
                503,
                response: null,
                new Dictionary<string, IEnumerable<string>>(),
                null));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Should().Match<Snackbar>(snackbar =>
                snackbar.Message == "The trainings could not be loaded. Try again in a moment."
                && snackbar.Severity == Severity.Error));
    }

    /// <summary>
    /// Renders, something else went wrong, does not turn the exception into interface copy.
    /// </summary>
    [Fact]
    public void Renders_SomethingElseWentWrong_DoesNotTurnTheExceptionIntoInterfaceCopy()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync())
            .ThrowsAsync(new InvalidOperationException("Object reference not set to an instance of an object."));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Something went wrong loading the trainings."));
    }

    private static TrainingResponseHttp Training(string title, params string[] topics) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Topics = [.. topics]
        };
}
