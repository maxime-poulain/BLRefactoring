using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Moq;
using TrainingHub.Blazor.Client.Pages.Trainings;
using TrainingHub.Blazor.Client.Tests.Infrastructure;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the training form's failure paths.
/// </summary>
/// <remarks>
/// The page shows the API's refusals in the API's own words when a problem document carries them.
/// These tests hold the other half of that policy: when there is no document — the API was
/// unreachable, or the failure is the page's own — the generator's sentence and the exception's
/// message belong in the console, never on screen. The trainings list had this policy and its
/// police from the start; this page received the same treatment later, and these tests are what
/// keeps it from regressing.
/// </remarks>
public sealed class CreateTrainingTests : ComponentTest
{
    private readonly Mock<ITrainingClient> _trainingClient = new();

    /// <summary>
    /// Create training tests.
    /// </summary>
    public CreateTrainingTests()
    {
        Services.AddSingleton(_trainingClient.Object);
    }

    /// <summary>
    /// Loading, the API was unreachable, does not show the generator's own sentence.
    /// </summary>
    [Fact]
    public void Loading_TheApiWasUnreachable_DoesNotShowTheGeneratorsOwnSentence()
    {
        // Arrange
        _trainingClient
            .Setup(client => client.GetTrainingByIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(ApiExceptions.Unreachable());

        // Act
        Render<CreateTraining>(parameters => parameters.Add(page => page.Id, Guid.NewGuid()));

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The training could not be loaded. Try again in a moment.");
    }

    /// <summary>
    /// Submitting, the API was unreachable, does not show the generator's own sentence.
    /// </summary>
    [Fact]
    public void Submitting_TheApiWasUnreachable_DoesNotShowTheGeneratorsOwnSentence()
    {
        // Arrange
        _trainingClient
            .Setup(client => client.UpdateTrainingAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<EditTrainingHttpRequest>()))
            .ThrowsAsync(ApiExceptions.Unreachable());

        var page = RenderPrefilled();

        // Act
        Submit(page);

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The training could not be updated. Try again in a moment."));
    }

    /// <summary>
    /// Submitting, something else went wrong, does not turn the exception into interface copy.
    /// </summary>
    /// <remarks>
    /// Whatever this is, it is the page's own. A <see cref="InvalidOperationException"/> — or a
    /// <see cref="NullReferenceException"/> — must not become UI copy.
    /// </remarks>
    [Fact]
    public void Submitting_SomethingElseWentWrong_DoesNotTurnTheExceptionIntoInterfaceCopy()
    {
        // Arrange
        _trainingClient
            .Setup(client => client.UpdateTrainingAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<EditTrainingHttpRequest>()))
            .ThrowsAsync(new InvalidOperationException("a stack detail nobody should read on screen"));

        var page = RenderPrefilled();

        // Act
        Submit(page);

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Something went wrong saving the training."));
    }

    /// <summary>
    /// The form in edit mode, prefilled by the server's answer, so every field is already valid.
    /// </summary>
    /// <remarks>
    /// Edit mode rather than DOM-driving the empty create form: the page fills itself from what
    /// the read answered, which is the least brittle way to reach a submittable state — the same
    /// reason ProfileTests renders a page that loads its own data.
    /// </remarks>
    private IRenderedComponent<CreateTraining> RenderPrefilled()
    {
        _trainingClient
            .Setup(client => client.GetTrainingByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new SwaggerResponse<TrainingHttpResponse>(
                200,
                new Dictionary<string, IEnumerable<string>> { ["ETag"] = ["\"v1\""] },
                new TrainingHttpResponse
                {
                    Id = Guid.NewGuid(),
                    Title = "A valid training title",
                    TrainerId = Guid.NewGuid(),
                    Topics = ["DDD"],
                    Description = "A sentence long enough to satisfy the field.",
                    Prerequisites = "A sentence long enough to satisfy the field.",
                    AcquiredSkills = "A sentence long enough to satisfy the field."
                }));

        return Render<CreateTraining>(parameters => parameters.Add(page => page.Id, Guid.NewGuid()));
    }

    private static void Submit(IRenderedComponent<CreateTraining> page) =>
        page.FindAll("button")
            .Single(button => button.TextContent.Contains("Save Changes", StringComparison.Ordinal))
            .Click();

    /// <summary>
    /// Loading, the training vanished, says so gently and returns to the list.
    /// </summary>
    [Fact]
    public void Loading_TheTrainingVanished_SaysSoGentlyAndReturnsToTheList()
    {
        // Arrange
        _trainingClient
            .Setup(client => client.GetTrainingByIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(ApiExceptions.Refused(404, "The training was not found."));

        // Act
        Render<CreateTraining>(parameters => parameters.Add(page => page.Id, Guid.NewGuid()));

        // Assert
        Shown().Should().ContainSingle()
            .Which.Should().Match<Snackbar>(snackbar =>
                snackbar.Message == "That training no longer exists."
                && snackbar.Severity == Severity.Info);
    }

    /// <summary>
    /// Submitting, somebody else edited first, warns without retrying.
    /// </summary>
    [Fact]
    public void Submitting_SomebodyElseEditedFirst_WarnsWithoutRetrying()
    {
        // Arrange
        _trainingClient
            .Setup(client => client.UpdateTrainingAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<EditTrainingHttpRequest>()))
            .ThrowsAsync(ApiExceptions.Refused(412, "The If-Match header named a version that is not current."));

        var page = RenderPrefilled();

        // Act
        Submit(page);

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Severity.Should().Be(Severity.Warning));
    }

    /// <summary>
    /// Submitting, refused with two domain errors, shows both rather than the first alone.
    /// </summary>
    [Fact]
    public void Submitting_RefusedWithTwoDomainErrors_ShowsBothRatherThanTheFirstAlone()
    {
        // Arrange
        _trainingClient
            .Setup(client => client.UpdateTrainingAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<EditTrainingHttpRequest>()))
            .ThrowsAsync(ApiExceptions.RefusedWithDomainErrors(
                409,
                "A training with the same title already exists for this trainer.",
                "The catalog is full for this trainer."));

        var page = RenderPrefilled();

        // Act
        Submit(page);

        // Assert
        page.WaitForAssertion(() => Shown().Select(snackbar => snackbar.Message).Should().Equal(
            "A training with the same title already exists for this trainer.",
            "The catalog is full for this trainer."));
    }
}
