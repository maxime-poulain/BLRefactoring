using Moq;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.Contact;
using TrainingHub.Shared;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behavior covered for the one write on the catalog: a visitor's message becoming an outbox row.
/// </summary>
/// <remarks>
/// Three outcomes and nothing else: an offered trainer gets the fact published and committed, a
/// trainer the catalog will not show gets the profile's own refusal, and a honeypot gets a success
/// that sends nothing — the last being the one worth watching, because a bot told apart is a bot
/// taught to try again with the field left blank (ADR 0082).
/// </remarks>
public sealed class ContactTrainerCommandHandlerTests
{
    private readonly Mock<ICatalogDetailQuery> _catalogDetail = new();
    private readonly Mock<IIntegrationEventPublisher> _integrationEvents = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    /// <summary>
    /// Handle, an offered trainer, publishes the fact as written and commits.
    /// </summary>
    /// <remarks>
    /// Field for field, because the consumer composes the email out of exactly this envelope: a
    /// swapped name or a dropped training identifier would surface as a wrong message in somebody's
    /// mailbox, long after every build was green.
    /// </remarks>
    [Fact]
    public async Task Handle_AnOfferedTrainer_PublishesTheFactAsWrittenAndCommits()
    {
        var trainerId = Guid.CreateVersion7();
        var trainingId = Guid.CreateVersion7();
        Offered(trainerId);

        var result = await Sut().Handle(Contact(trainerId, trainingId), CancellationToken.None);

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
    /// Handle, a trainer the catalog will not show, answers the profile's refusal and publishes
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The same composed visibility as the profile's 404 (ADR 0062, ADR 0070): a trainer nobody may
    /// look at is a trainer nobody may write to, and the read comes before the write so no fact is
    /// recorded about them.
    /// </remarks>
    [Fact]
    public async Task Handle_ATrainerTheCatalogWillNotShow_AnswersNotFoundAndPublishesNothing()
    {
        var result = await Sut().Handle(Contact(Guid.CreateVersion7(), trainingId: null), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
        _integrationEvents.Verify(publisher => publisher.PublishAsync(
                It.IsAny<TrainerContactedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, the honeypot filled, answers success and touches nothing at all.
    /// </summary>
    /// <remarks>
    /// Not even the read: a request known to be automated is answered before any work is done on
    /// it, and answered exactly as a good one is, because a different answer is how a bot finds
    /// out (ADR 0082).
    /// </remarks>
    [Fact]
    public async Task Handle_TheHoneypotFilled_AnswersSuccessAndTouchesNothing()
    {
        var command = new ContactTrainerCommand
        {
            TrainerId = Guid.CreateVersion7(),
            SenderFirstname = "Grace",
            SenderLastname = "Hopper",
            SenderEmailAddress = "grace@example.org",
            Message = "I would like to book this training.",
            LooksAutomated = true
        };

        var result = await Sut().Handle(command, CancellationToken.None);

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

    private ContactTrainerCommandHandler Sut() =>
        new(_catalogDetail.Object, _integrationEvents.Object, _unitOfWork.Object);

    private void Offered(Guid trainerId) =>
        _catalogDetail
            .Setup(detail => detail.FindOfferedTrainerAsync(trainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogTrainerDto
            {
                Id = trainerId,
                Firstname = "Ada",
                Lastname = "Lovelace",
                Trainings = []
            });

    private static ContactTrainerCommand Contact(Guid trainerId, Guid? trainingId) => new()
    {
        TrainerId = trainerId,
        TrainingId = trainingId,
        SenderFirstname = "Grace",
        SenderLastname = "Hopper",
        SenderEmailAddress = "grace@example.org",
        Message = "I would like to book this training."
    };
}
