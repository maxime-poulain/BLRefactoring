using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Pages.Administration;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages.Administration;

/// <summary>
/// Behavior covered for the page an administrator lands on.
/// </summary>
/// <remarks>
/// It exists because the alternative was landing them on the trainer's space, a surface their
/// account has no standing on at all (ADR 0078). What is worth pinning is that it asks no new
/// question of the API — every number comes from a list the administration already publishes,
/// asked for one row so the total can be read off it — and that one unreadable count degrades its
/// own card rather than the page.
/// </remarks>
public sealed class AdministrationHomeTests : ComponentTest
{
    private readonly Mock<IAdministrationClient> _administration = new();
    private readonly Mock<IOutboxClient> _outbox = new();

    /// <summary>Administration home tests.</summary>
    public AdministrationHomeTests()
    {
        Services.AddSingleton(_administration.Object);
        Services.AddSingleton(_outbox.Object);

        _administration
            .Setup(client => client.GetTrainersAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Trainers(0));
        _administration
            .Setup(client => client.GetTrainingsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Trainings(0));
        _outbox
            .Setup(client => client.GetPoisonAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Poison(0));
    }

    /// <summary>
    /// Renders, asks each list for one row and reads the total off it.
    /// </summary>
    /// <remarks>
    /// One row rather than a page: the rows are never rendered, and asking for twenty of them to
    /// read a number the server puts in the envelope is twenty rows of nothing.
    /// </remarks>
    [Fact]
    public void Renders_AsksEachListForOneRow_AndReadsTheTotalOffIt()
    {
        // Act
        Render<AdministrationHome>();

        // Assert
        _administration.Verify(client => client.GetTrainersAsync(null, null, 1, 1), Times.Once);
        _administration.Verify(client => client.GetTrainingsAsync(null, null, 1, 1), Times.Once);
        _outbox.Verify(client => client.GetPoisonAsync(1, 1), Times.Once);
    }

    /// <summary>
    /// Renders, asks each surface what is waiting on a decision.
    /// </summary>
    /// <remarks>
    /// The two words the domain publishes, compared ordinally by the server (ADR 0050). They are
    /// the point of the page rather than decoration: a total says how much there is, and these say
    /// how much of it somebody has already acted on.
    /// </remarks>
    [Fact]
    public void Renders_AsksEachSurfaceWhatIsWaitingOnADecision()
    {
        // Act
        Render<AdministrationHome>();

        // Assert
        _administration.Verify(client => client.GetTrainersAsync("Suspended", null, 1, 1), Times.Once);
        _administration.Verify(client => client.GetTrainingsAsync("Withheld", null, 1, 1), Times.Once);
    }

    /// <summary>
    /// Rendered, the counts read, shows them with the door each one opens.
    /// </summary>
    [Fact]
    public void Rendered_TheCountsRead_ShowsThemWithTheDoorEachOneOpens()
    {
        // Arrange
        _administration
            .Setup(client => client.GetTrainersAsync(null, null, 1, 1))
            .ReturnsAsync(Trainers(12));
        _administration
            .Setup(client => client.GetTrainingsAsync(null, null, 1, 1))
            .ReturnsAsync(Trainings(48));
        _outbox
            .Setup(client => client.GetPoisonAsync(1, 1))
            .ReturnsAsync(Poison(3));

        // Act
        var page = Render<AdministrationHome>();

        // Assert
        page.WaitForAssertion(() => page.Markup.Should().Contain("12").And.Contain("48").And.Contain("3"));

        page.FindAll("a").Select(anchor => anchor.GetAttribute("href"))
            .Should().Contain("/administration/trainers")
            .And.Contain("/administration/trainings")
            .And.Contain("/administration/outbox");
    }

    /// <summary>
    /// Rendered, one count unreadable, says it does not know rather than zero.
    /// </summary>
    /// <remarks>
    /// A zero is a claim, and the wrong one: "no trainer is suspended" and "the suspended trainers
    /// could not be counted" are different sentences, and only one of them should let an operator
    /// stop looking.
    /// </remarks>
    [Fact]
    public void Rendered_OneCountUnreadable_SaysItDoesNotKnowRatherThanZero()
    {
        // Arrange
        _administration
            .Setup(client => client.GetTrainersAsync(null, null, 1, 1))
            .ReturnsAsync(Trainers(12));
        _outbox
            .Setup(client => client.GetPoisonAsync(1, 1))
            .ThrowsAsync(new ApiException(
                "The HTTP status code of the response was not expected (503).",
                503,
                "",
                new Dictionary<string, IEnumerable<string>>(),
                null));

        // Act
        var page = Render<AdministrationHome>();

        // Assert
        page.WaitForAssertion(() => page.Markup.Should().Contain("—",
            "an unreadable count is a dash, and the other cards keep their numbers"));

        page.Markup.Should().Contain("12");
    }

    private static PagedHttpResponseOfAdministrationTrainerHttpResponse Trainers(int totalCount) =>
        new()
        {
            Items = [],
            Page = 1,
            PageSize = 1,
            TotalCount = totalCount,
            TotalPages = 1,
            HasNextPage = false,
            HasPreviousPage = false
        };

    private static PagedHttpResponseOfAdministrationTrainingHttpResponse Trainings(int totalCount) =>
        new()
        {
            Items = [],
            Page = 1,
            PageSize = 1,
            TotalCount = totalCount,
            TotalPages = 1,
            HasNextPage = false,
            HasPreviousPage = false
        };

    private static PagedHttpResponseOfPoisonedMessageHttpResponse Poison(int totalCount) =>
        new()
        {
            Items = [],
            Page = 1,
            PageSize = 1,
            TotalCount = totalCount,
            TotalPages = 1,
            HasNextPage = false,
            HasPreviousPage = false
        };
}
