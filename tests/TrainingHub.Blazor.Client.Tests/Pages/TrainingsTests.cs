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
/// read them from the network tab. It now asks the server for its own — one page at a time since
/// ADR 0029, so the pager is part of the behaviour: without it, everything past the first page
/// would be unreachable, which is the old unbounded read's defect wearing the new contract.
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

        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 0));
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
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 2,
                Training("Domain-Driven Design", "Programming", "Business"),
                Training("Public speaking", "Leadership")));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Markup.Should().NotContain("No trainings yet");
        page.Markup.Should()
            .Contain("Domain-Driven Design").And.Contain("Public speaking")
            .And.Contain("Programming").And.Contain("Business").And.Contain("Leadership");
    }

    /// <summary>
    /// Renders, a published training, badges nothing and offers to withdraw it.
    /// </summary>
    /// <remarks>
    /// Published is what a training is unless something happened to it, so the card says nothing —
    /// a badge on every card would make the one badge that matters harder to see.
    /// </remarks>
    [Fact]
    public void Renders_APublishedTraining_BadgesNothingAndOffersToWithdrawIt()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 1,
                Training("Domain-Driven Design", "Programming")));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Markup.Should().NotContain("Unpublished");
        page.Markup.Should().Contain(Icons.Material.Filled.VisibilityOff);
    }

    /// <summary>
    /// Renders, a withdrawn training, says so and offers to publish it again.
    /// </summary>
    /// <remarks>
    /// The pair of buttons is what makes the lifecycle usable rather than merely modelled: a
    /// trainer who withdrew a training and thought better of it gets it back without recreating it.
    /// </remarks>
    [Fact]
    public void Renders_AWithdrawnTraining_SaysSoAndOffersToPublishItAgain()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 1,
                WithdrawnTraining("Public speaking", "Leadership")));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Markup.Should().Contain("Unpublished");
        page.Markup.Should().Contain(Icons.Material.Filled.Visibility);
    }

    /// <summary>
    /// Renders, a published training, keeps the default card surface.
    /// </summary>
    /// <remarks>
    /// The pair of facts below is what makes the status readable across a whole page rather than
    /// card by card. This half asserts the absence: published is the ordinary state, so its card is
    /// the one nothing was done to, and a treatment applied to every card would say nothing about
    /// any of them.
    /// </remarks>
    [Fact]
    public void Renders_APublishedTraining_KeepsTheDefaultCardSurface()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 1,
                Training("Domain-Driven Design", "Programming")));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Find("div.mud-card").ClassList.Should().NotContain(RecessedSurface);
    }

    /// <summary>
    /// Renders, a withdrawn training, sets its card back on the theme's recessed surface.
    /// </summary>
    /// <remarks>
    /// Asserted on the theme's class rather than on a colour, because that is what the page is
    /// allowed to say: there is no stylesheet in this project and the layout toggles dark mode, so
    /// a test that pinned a hex would pin the wrong one half the time. Colour is not the only
    /// signal — the sibling fact above checks the word is on the card too.
    /// </remarks>
    [Fact]
    public void Renders_AWithdrawnTraining_SetsItsCardBackOnTheRecessedSurface()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 1,
                WithdrawnTraining("Public speaking", "Leadership")));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Find("div.mud-card").ClassList.Should().Contain(RecessedSurface);
    }

    /// <summary>
    /// Renders, a mixed page, tells the two apart card by card.
    /// </summary>
    /// <remarks>
    /// The two facts above each render one card, so either would pass against a page that gave
    /// every card the same treatment. This one is the one that fails if the status is read once for
    /// the page rather than once per training.
    /// </remarks>
    [Fact]
    public void Renders_AMixedPage_TellsTheTwoApartCardByCard()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 2,
                Training("Domain-Driven Design", "Programming"),
                WithdrawnTraining("Public speaking", "Leadership")));

        // Act
        var page = Render<Trainings>();

        // Assert
        var cards = page.FindAll("div.mud-card");
        cards.Should().HaveCount(2);
        cards[0].ClassList.Should().NotContain(RecessedSurface);
        cards[1].ClassList.Should().Contain(RecessedSurface);
    }

    /// <summary>
    /// Renders, offers no way to delete a training.
    /// </summary>
    /// <remarks>
    /// Deleting stays on the API — for the training created by mistake, and for erasure on request
    /// — and leaves the interface entirely. Withdrawing is the everyday act now, and a button that
    /// destroys a catalogue entry should not sit where the everyday one used to (ADR 0050).
    /// </remarks>
    [Fact]
    public void Renders_OffersNoWayToDeleteATraining()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 1,
                Training("Domain-Driven Design", "Programming")));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Markup.Should().NotContain(Icons.Material.Filled.Delete);
    }

    /// <summary>
    /// Renders, asks the server for its own trainings rather than for all of them.
    /// </summary>
    /// <remarks>
    /// The whole reason this page changed. Reading the trainer separately and comparing owners in
    /// the markup is what it replaced, and nothing about the rendered page would look different if
    /// it came back. The size travels unset on purpose: the default is the server's to choose and
    /// the cap the server's to hold, so the page names only the coordinate it owns.
    /// </remarks>
    [Fact]
    public void Renders_AsksTheServerForItsOwnTrainings()
    {
        // Act
        Render<Trainings>();

        // Assert
        _trainings.Verify(client => client.GetMineAsync(1, null), Times.Once);
    }

    /// <summary>
    /// Renders, one page of trainings, shows no pager.
    /// </summary>
    [Fact]
    public void Renders_OnePageOfTrainings_ShowsNoPager()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(page: 1, totalPages: 1, totalCount: 1, Training("Domain-Driven Design")));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.Markup.Should().NotContain("mud-pagination");
    }

    /// <summary>
    /// Walking to another page, asks the server for that page.
    /// </summary>
    [Fact]
    public void WalkingToAnotherPage_AsksTheServerForThatPage()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync((int? requested, int? _) =>
                Page(page: requested ?? 1, totalPages: 2, totalCount: 21, Training("Domain-Driven Design")));

        var page = Render<Trainings>();

        // Act
        page.FindAll("button").Single(button => button.TextContent.Trim() == "2").Click();

        // Assert
        page.WaitForAssertion(() => _trainings.Verify(client => client.GetMineAsync(2, null), Times.Once));
    }

    /// <summary>
    /// Standing past the last page, steps back onto the page that still exists.
    /// </summary>
    /// <remarks>
    /// Deleting the only training of the last page answers a page past the end — empty, with a
    /// total that no longer reaches it. Staying there would show the empty state to a trainer who
    /// still has trainings.
    /// </remarks>
    [Fact]
    public void StandingPastTheLastPage_StepsBackOntoThePageThatStillExists()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync((int? requested, int? _) => requested > 1
                ? Page(page: requested ?? 1, totalPages: 1, totalCount: 1)
                : Page(page: 1, totalPages: 2, totalCount: 21, Training("Domain-Driven Design")));

        var page = Render<Trainings>();

        // Act — the server answers page 2 empty, claiming one page in total.
        page.FindAll("button").Single(button => button.TextContent.Trim() == "2").Click();

        // Assert — the page notices and asks for the last page there is.
        page.WaitForAssertion(() => _trainings.Verify(client => client.GetMineAsync(1, null), Times.Exactly(2)));
        page.WaitForAssertion(() => page.Markup.Should().Contain("Domain-Driven Design"));
    }

    /// <summary>
    /// Renders, the request was refused, shows the document's own words.
    /// </summary>
    [Fact]
    public void Renders_TheRequestWasRefused_ShowsTheDocumentsOwnWords()
    {
        // Arrange
        _trainings
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
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
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
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
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("Object reference not set to an instance of an object."));

        // Act
        var page = Render<Trainings>();

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Something went wrong loading the trainings."));
    }

    private static PagedHttpResponseOfTrainingHttpResponse Page(
        int page,
        int totalPages,
        int totalCount,
        params TrainingHttpResponse[] trainings) =>
        new()
        {
            Items = [.. trainings],
            Page = page,
            PageSize = 20,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };

    // MudBlazor's own utility for the theme's recessed surface, which resolves to
    // --mud-palette-background-gray and therefore follows the layout's dark mode toggle. Named
    // once so the page and its tests cannot drift onto two different words for one decision.
    private const string RecessedSurface = "mud-background-gray";

    private static TrainingHttpResponse Training(string title, params string[] topics) =>
        WithStatus(title, "Published", topics);

    private static TrainingHttpResponse WithdrawnTraining(string title, params string[] topics) =>
        WithStatus(title, "Unpublished", topics);

    // Not an overload of Training: two methods differing only by a leading string let overload
    // resolution bind the first topic as the status, and the test that caught it did so by
    // rendering an "Unpublished" badge on a training nobody had withdrawn.
    private static TrainingHttpResponse WithStatus(string title, string status, string[] topics) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Topics = [.. topics],
            Status = status
        };
}
