using Moq;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for <c>SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler</c>.
/// </summary>
/// <remarks>
/// The consumer routes and the composer writes, so what is held here is the routing: the trainer's
/// name reaches the composer, the fact's language decides which words come back, and the published
/// contact address receives them unread (ADR 0091). The prose itself is
/// <c>NotificationComposerTests</c>'.
/// </remarks>
public sealed class SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandlerTests
{
    private readonly Mock<INotificationComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, sends the welcome email to the created trainer.
    /// </summary>
    [Fact]
    public async Task Handle_SendsTheWelcomeEmail_ToTheCreatedTrainer()
    {
        // Arrange
        var fact = new TrainerCreatedIntegrationEvent(
            Guid.NewGuid(), "John", "Doe", "john.doe@example.com", "fr");

        _composer
            .Setup(composer => composer.Welcome("fr", "John Doe"))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler(
            _composer.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(fact, CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "john.doe@example.com"
                    && message.Subject == "the composed subject"
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
