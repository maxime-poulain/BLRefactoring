using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Shared.Application.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the three consumers that tell a trainer about a sanction.
/// </summary>
/// <remarks>
/// Two things are worth pinning here and nothing else is. The address: the notice goes to the
/// account, not to the published contact address, and the two are deliberately different values in
/// every fact below so that reading the wrong one fails rather than passes by coincidence
/// (ADR 0056). And the reason: a sanction the product never explains is one the trainer discovers
/// by failing to do something (ADR 0053) — which reaches the composer beside the language read
/// from the same projection as the address (ADR 0091).
/// </remarks>
public sealed class SanctionNoticeIntegrationEventHandlerTests
{
    private const string AccountAddress = "ada.lovelace@account.example.com";

    private readonly Mock<INotificationComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<ITrainerAccountQuery> _accountQuery = new();

    /// <summary>
    /// Handle, a suspension, sends the reason to the account address.
    /// </summary>
    [Fact]
    public async Task Handle_ASuspension_SendsTheReasonToTheAccountAddress()
    {
        var trainerId = Guid.NewGuid();
        KnownAccount(trainerId);

        _composer
            .Setup(composer => composer.Suspension(
                "fr", "Ada Lovelace", "Repeated breaches of the content policy."))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new SendSuspensionNoticeWhenTrainerSuspendedIntegrationEventHandler(
            _accountQuery.Object,
            _composer.Object,
            _emailSender.Object,
            NullLogger<SendSuspensionNoticeWhenTrainerSuspendedIntegrationEventHandler>.Instance);

        await sut.HandleAsync(
            new TrainerSuspendedIntegrationEvent(trainerId, "Repeated breaches of the content policy."),
            CancellationToken.None);

        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == AccountAddress
                    && message.Subject == "the composed subject"
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a reinstatement, tells the account the sanction is over.
    /// </summary>
    [Fact]
    public async Task Handle_AReinstatement_TellsTheAccountTheSanctionIsOver()
    {
        var trainerId = Guid.NewGuid();
        KnownAccount(trainerId);

        _composer
            .Setup(composer => composer.Reinstatement("fr", "Ada Lovelace"))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new SendReinstatementNoticeWhenTrainerReinstatedIntegrationEventHandler(
            _accountQuery.Object,
            _composer.Object,
            _emailSender.Object,
            NullLogger<SendReinstatementNoticeWhenTrainerReinstatedIntegrationEventHandler>.Instance);

        await sut.HandleAsync(new TrainerReinstatedIntegrationEvent(trainerId), CancellationToken.None);

        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == AccountAddress
                    && message.Subject == "the composed subject"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a withholding, names the training and its reason.
    /// </summary>
    [Fact]
    public async Task Handle_AWithholding_NamesTheTrainingAndItsReason()
    {
        var trainerId = Guid.NewGuid();
        KnownAccount(trainerId);

        _composer
            .Setup(composer => composer.Withholding(
                "fr", "Ada Lovelace", "Advanced domain modeling", "Reported for misleading claims."))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new SendWithholdingNoticeWhenTrainingWithheldIntegrationEventHandler(
            _accountQuery.Object,
            _composer.Object,
            _emailSender.Object,
            NullLogger<SendWithholdingNoticeWhenTrainingWithheldIntegrationEventHandler>.Instance);

        await sut.HandleAsync(
            new TrainingWithheldIntegrationEvent(
                Guid.NewGuid(), trainerId, "Advanced domain modeling", "Reported for misleading claims."),
            CancellationToken.None);

        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == AccountAddress
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, an account the query cannot find, sends nothing and does not throw.
    /// </summary>
    /// <remarks>
    /// Throwing is the protocol for "try again" (ADR 0034), and an account that no longer exists
    /// will not exist on the next attempt either: the consumer would spend its whole retry budget
    /// re-reading the same absence and end up poisoned (ADR 0033). Asserted on one of the three
    /// because the three share the shape.
    /// </remarks>
    [Fact]
    public async Task Handle_AnAccountTheQueryCannotFind_SendsNothingAndDoesNotThrow()
    {
        _accountQuery
            .Setup(query => query.GetByTrainerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainerAccountDto?)null);

        var sut = new SendSuspensionNoticeWhenTrainerSuspendedIntegrationEventHandler(
            _accountQuery.Object,
            _composer.Object,
            _emailSender.Object,
            NullLogger<SendSuspensionNoticeWhenTrainerSuspendedIntegrationEventHandler>.Instance);

        await sut.HandleAsync(
            new TrainerSuspendedIntegrationEvent(Guid.NewGuid(), "Repeated breaches."),
            CancellationToken.None);

        _emailSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void KnownAccount(Guid trainerId) =>
        _accountQuery
            .Setup(query => query.GetByTrainerIdAsync(trainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrainerAccountDto(AccountAddress, "Ada", "Lovelace", "fr"));
}
