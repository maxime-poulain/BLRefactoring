using AwesomeAssertions;
using Moq;
using TrainingHub.DDD.Application.Services.CatalogServices;
using TrainingHub.Shared;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services;

/// <summary>
/// Behavior covered for <c>CatalogApplicationService</c>.
/// </summary>
/// <remarks>
/// The layered half of the catalog search, and the twin of <c>SearchCatalogQueryHandler</c>'s
/// facts on purpose: what ADR 0029 asks of the two hosts is that they answer the same question the
/// same way, and over a read model that means arriving at the same port with the same arguments.
/// </remarks>
public sealed class CatalogApplicationServiceTests
{
    private readonly Mock<ITrainingSearchQuery> _trainingSearch = new();
    private readonly Mock<ICatalogDetailQuery> _catalogDetail = new();
    private readonly Mock<IIntegrationEventPublisher> _integrationEvents = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    /// <summary>The service under test, built over the four substitutes above.</summary>
    private CatalogApplicationService Sut() => new(
        _trainingSearch.Object, _catalogDetail.Object, _integrationEvents.Object, _unitOfWork.Object);

    /// <summary>
    /// Search async, a whole question, asks the index for exactly it.
    /// </summary>
    [Fact]
    public async Task SearchAsync_AWholeQuestion_AsksTheIndexForExactlyIt()
    {
        var paging = new PageRequest { Page = 2, PageSize = 10 };

        _trainingSearch
            .Setup(search => search.SearchAsync(
                It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CatalogOrder>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CatalogTrainingDto>([], 2, 10, 0));

        var sut = Sut();

        await sut.SearchAsync(new CatalogSearchRequest
        {
            Term = "domain",
            Topics = ["Programming"],
            Order = CatalogOrder.Newest,
            Paging = paging
        });

        _trainingSearch.Verify(
            search => search.SearchAsync(
                "domain", new[] { "Programming" }, CatalogOrder.Newest, paging, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Search async, the page the index answered, hands it back untouched.
    /// </summary>
    /// <remarks>
    /// No mapping happens here, unlike every other listing on this stack: the index already answers
    /// the read model, so there are no aggregates to turn into one (ADR 0059).
    /// </remarks>
    [Fact]
    public async Task SearchAsync_ThePageTheIndexAnswered_HandsItBackUntouched()
    {
        var answered = new PagedResult<CatalogTrainingDto>(
            [new CatalogTrainingDto { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), Title = "Found" }],
            Page: 1,
            PageSize: 20,
            TotalCount: 1);

        _trainingSearch
            .Setup(search => search.SearchAsync(
                It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CatalogOrder>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = Sut();

        var page = await sut.SearchAsync(new CatalogSearchRequest());

        page.Should().BeSameAs(answered);
    }

    /// <summary>
    /// Find offered async, an identifier, asks the detail port for exactly it.
    /// </summary>
    /// <remarks>
    /// And asks the <em>detail</em> port rather than the search one. The two questions look alike
    /// enough to be answered from one place, and the whole of ADR 0062 is the argument that they
    /// must not be: a search reads the index, a reading composes the index and the write model.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnIdentifier_AsksTheDetailPortForExactlyIt()
    {
        var trainingId = Guid.CreateVersion7();

        var sut = Sut();

        await sut.FindOfferedAsync(trainingId);

        _catalogDetail.Verify(
            detail => detail.FindOfferedAsync(trainingId, It.IsAny<CancellationToken>()),
            Times.Once);

        _trainingSearch.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Find offered async, nothing on offer, hands the nothing back.
    /// </summary>
    /// <remarks>
    /// The absence travels rather than becoming a refusal here: what a missing training means to a
    /// caller is the action's decision, and it answers 404 (ADR 0055).
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_NothingOnOffer_HandsTheNothingBack()
    {
        _catalogDetail
            .Setup(detail => detail.FindOfferedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogTrainingDetailDto?)null);

        var sut = Sut();

        var offered = await sut.FindOfferedAsync(Guid.CreateVersion7());

        offered.Should().BeNull();
    }

    /// <summary>
    /// Find offered trainer async, an identifier, asks the detail port for exactly it.
    /// </summary>
    /// <remarks>
    /// The profile arrives at the same port as the detail (ADR 0070), and never at the search one:
    /// whether a person is offering is the index's composition, read where ADR 0062 keeps it.
    /// </remarks>
    [Fact]
    public async Task FindOfferedTrainerAsync_AnIdentifier_AsksTheDetailPortForExactlyIt()
    {
        var trainerId = Guid.CreateVersion7();

        var sut = Sut();

        await sut.FindOfferedTrainerAsync(trainerId);

        _catalogDetail.Verify(
            detail => detail.FindOfferedTrainerAsync(trainerId, It.IsAny<CancellationToken>()),
            Times.Once);

        _trainingSearch.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Find offered trainer async, nobody to show, hands the nothing back.
    /// </summary>
    [Fact]
    public async Task FindOfferedTrainerAsync_NobodyToShow_HandsTheNothingBack()
    {
        _catalogDetail
            .Setup(detail => detail.FindOfferedTrainerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogTrainerDto?)null);

        var sut = Sut();

        var profile = await sut.FindOfferedTrainerAsync(Guid.CreateVersion7());

        profile.Should().BeNull();
    }

    /// <summary>
    /// Get facets async, hands back what the index answered.
    /// </summary>
    /// <remarks>
    /// The same pass-through the search is, and honestly so (ADR 0049, ADR 0069): over a read
    /// model there is nothing for the two stacks to differ about.
    /// </remarks>
    [Fact]
    public async Task GetFacetsAsync_HandsBackWhatTheIndexAnswered()
    {
        var facets = new List<TopicFacetDto> { new() { Topic = "Design", OfferedCount = 2 } };

        _trainingSearch
            .Setup(search => search.FacetsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(facets);

        var sut = Sut();

        (await sut.GetFacetsAsync(term: null)).Should().BeSameAs(facets);
    }

    /// <summary>
    /// Contact async, an offered trainer, publishes the fact as written and commits.
    /// </summary>
    /// <remarks>
    /// Field for field, because the consumer composes the email out of exactly this envelope: a
    /// swapped name or a dropped training identifier would surface as a wrong message in
    /// somebody's mailbox, long after every build was green (ADR 0082).
    /// </remarks>
    [Fact]
    public async Task ContactAsync_AnOfferedTrainer_PublishesTheFactAsWrittenAndCommits()
    {
        var trainerId = Guid.CreateVersion7();
        var trainingId = Guid.CreateVersion7();
        OfferedTrainer(trainerId);

        var result = await Sut().ContactAsync(Contact(trainerId, trainingId));

        result.ShouldBeSuccess();
        _integrationEvents.Verify(publisher => publisher.PublishAsync(
                It.Is<TrainerContactedIntegrationEvent>(fact =>
                    fact.TrainerId == trainerId
                    && fact.TrainingId == trainingId
                    && fact.SenderFirstname == "Grace"
                    && fact.SenderLastname == "Hopper"
                    && fact.SenderEmailAddress == "grace@example.org"
                    && fact.Message == "I would like to book this training."),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Contact async, a trainer the catalog will not show, answers not found and publishes nothing.
    /// </summary>
    /// <remarks>
    /// The same composed visibility as the profile's 404 (ADR 0062, ADR 0070): a trainer nobody
    /// may look at is a trainer nobody may write to, and the read comes before the write so no
    /// fact is recorded about them.
    /// </remarks>
    [Fact]
    public async Task ContactAsync_ATrainerTheCatalogWillNotShow_AnswersNotFoundAndPublishesNothing()
    {
        var result = await Sut().ContactAsync(Contact(Guid.CreateVersion7(), trainingId: null));

        result.ShouldContainError(ErrorCodes.NotFound);
        _integrationEvents.Verify(publisher => publisher.PublishAsync(
                It.IsAny<TrainerContactedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Contact async, the honeypot filled, answers success and touches nothing at all.
    /// </summary>
    /// <remarks>
    /// Not even the read: a request known to be automated is answered before any work is done on
    /// it, and answered exactly as a good one is, because a different answer is how a bot finds
    /// out (ADR 0082).
    /// </remarks>
    [Fact]
    public async Task ContactAsync_TheHoneypotFilled_AnswersSuccessAndTouchesNothing()
    {
        var result = await Sut().ContactAsync(new TrainerContactRequest
        {
            TrainerId = Guid.CreateVersion7(),
            SenderFirstname = "Grace",
            SenderLastname = "Hopper",
            SenderEmailAddress = "grace@example.org",
            Message = "I would like to book this training.",
            LooksAutomated = true
        });

        result.ShouldBeSuccess();
        _catalogDetail.Verify(detail => detail.FindOfferedTrainerAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _integrationEvents.Verify(publisher => publisher.PublishAsync(
                It.IsAny<TrainerContactedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void OfferedTrainer(Guid trainerId) =>
        _catalogDetail
            .Setup(detail => detail.FindOfferedTrainerAsync(trainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogTrainerDto
            {
                Id = trainerId,
                Firstname = "Ada",
                Lastname = "Lovelace",
                Trainings = []
            });

    private static TrainerContactRequest Contact(Guid trainerId, Guid? trainingId) => new()
    {
        TrainerId = trainerId,
        TrainingId = trainingId,
        SenderFirstname = "Grace",
        SenderLastname = "Hopper",
        SenderEmailAddress = "grace@example.org",
        Message = "I would like to book this training."
    };
}
