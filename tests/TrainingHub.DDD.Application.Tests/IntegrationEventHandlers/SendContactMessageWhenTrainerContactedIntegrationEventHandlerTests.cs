using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Shared.Application.Queries;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the consumer that carries a visitor's message to a trainer.
/// </summary>
/// <remarks>
/// The one place in this application that reads the trainer's contact address, and the facts pin
/// what makes that safe to be: the message goes to the address the port answered, the reply goes
/// to the visitor, and a trainer who no longer exists gets nothing sent about them rather than a
/// poison message retried until its budget is gone (ADR 0033, ADR 0082).
/// </remarks>
public sealed class SendContactMessageWhenTrainerContactedIntegrationEventHandlerTests
{
    private const string ContactAddress = "ada.published@example.com";

    private readonly Mock<ITrainerContactQuery> _contactQuery = new();
    private readonly Mock<ICatalogDetailQuery> _catalogDetail = new();
    private readonly Mock<INotificationComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, a known trainer, sends to the contact address with the reply going to the visitor.
    /// </summary>
    /// <remarks>
    /// The two addresses on the message each do their one job: the recipient is what the trainer
    /// published for being written to — resolved here, at delivery, never carried on the fact —
    /// and the <c>Reply-To</c> is the visitor, so answering the mail answers the person
    /// (ADR 0082). The words are the composer's, asked for in the trainer's own language and in
    /// nobody else's — the visitor's culture never reaches this message (ADR 0091).
    /// </remarks>
    [Fact]
    public async Task Handle_AKnownTrainer_SendsToTheContactAddressWithReplyToTheVisitor()
    {
        var trainerId = Guid.CreateVersion7();
        KnownTrainer(trainerId);
        Composes(about: null);

        await Sut().HandleAsync(Contacted(trainerId, trainingId: null), CancellationToken.None);

        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == ContactAddress
                    && message.ReplyTo == "grace@example.org"
                    && message.Subject == "the composed subject"
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, the training still offered, hands its title to the composer.
    /// </summary>
    /// <remarks>
    /// Read through the catalog's port at delivery rather than carried on the fact, so a title is
    /// never stored stale in the outbox: what the message names is what the catalog offers at the
    /// moment of sending.
    /// </remarks>
    [Fact]
    public async Task Handle_TheTrainingStillOffered_HandsItsTitleToTheComposer()
    {
        var trainerId = Guid.CreateVersion7();
        var trainingId = Guid.CreateVersion7();
        KnownTrainer(trainerId);
        _catalogDetail
            .Setup(detail => detail.FindOfferedAsync(trainingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogTrainingDetailDto
            {
                Id = trainingId,
                Title = "Domain Driven Design",
                TrainerName = "Ada Lovelace",
                TrainerId = trainerId,
                Topics = [],
                Description = "What a bounded context is.",
                Prerequisites = "None.",
                AcquiredSkills = "Drawing a context map."
            });

        Composes(about: "Domain Driven Design");

        await Sut().HandleAsync(Contacted(trainerId, trainingId), CancellationToken.None);

        _composer.Verify(
            composer => composer.ContactMessage(
                "ru",
                "Ada Lovelace",
                "Grace Hopper",
                "grace@example.org",
                "I would like to book this training.",
                "Domain Driven Design"),
            Times.Once);
    }

    /// <summary>
    /// Handle, a training withdrawn since, still carries the message without naming it.
    /// </summary>
    /// <remarks>
    /// The mention is what a withdrawal costs, never the message: what the visitor wrote is theirs
    /// and still worth carrying, and a consumer that threw on the absent title would poison a
    /// perfectly deliverable message.
    /// </remarks>
    [Fact]
    public async Task Handle_ATrainingWithdrawnSince_StillCarriesTheMessageWithoutNamingIt()
    {
        var trainerId = Guid.CreateVersion7();
        KnownTrainer(trainerId);
        Composes(about: null);

        await Sut().HandleAsync(Contacted(trainerId, Guid.CreateVersion7()), CancellationToken.None);

        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == ContactAddress
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a trainer who no longer exists, sends nothing.
    /// </summary>
    /// <remarks>
    /// Not thrown, deliberately: a deleted trainer is not a transient failure, and an exception
    /// here would spend the consumer's whole retry budget re-reading the same absence before
    /// parking the message as poison (ADR 0033).
    /// </remarks>
    [Fact]
    public async Task Handle_ATrainerWhoNoLongerExists_SendsNothing()
    {
        await Sut().HandleAsync(Contacted(Guid.CreateVersion7(), trainingId: null), CancellationToken.None);

        _emailSender.Verify(sender => sender.SendAsync(
                It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SendContactMessageWhenTrainerContactedIntegrationEventHandler Sut() =>
        new(
            _contactQuery.Object,
            _catalogDetail.Object,
            _composer.Object,
            _emailSender.Object,
            NullLogger<SendContactMessageWhenTrainerContactedIntegrationEventHandler>.Instance);

    private void KnownTrainer(Guid trainerId) =>
        _contactQuery
            .Setup(query => query.GetByTrainerIdAsync(trainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrainerContactDto(ContactAddress, "Ada", "Lovelace", "ru"));

    private void Composes(string? about) =>
        _composer
            .Setup(composer => composer.ContactMessage(
                "ru",
                "Ada Lovelace",
                "Grace Hopper",
                "grace@example.org",
                "I would like to book this training.",
                about))
            .Returns(new Notification("the composed subject", "the composed body"));

    private static TrainerContactedIntegrationEvent Contacted(Guid trainerId, Guid? trainingId) =>
        new(
            trainerId,
            trainingId,
            "Grace",
            "Hopper",
            "grace@example.org",
            "I would like to book this training.");
}
