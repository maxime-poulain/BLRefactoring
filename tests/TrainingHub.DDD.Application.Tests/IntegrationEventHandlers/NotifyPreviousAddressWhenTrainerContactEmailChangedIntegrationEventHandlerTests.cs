using Moq;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for <c>NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler</c>.
/// </summary>
public sealed class NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandlerTests
{
    private readonly Mock<INotificationComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, warns the old address, and names the new one.
    /// </summary>
    [Fact]
    public async Task Handle_WarnsTheOldAddress_AndNamesTheNewOne()
    {
        // Arrange
        var fact = new TrainerContactEmailChangedIntegrationEvent(
            Guid.NewGuid(), "old@example.com", "new@example.com", "ru");

        _composer
            .Setup(composer => composer.ContactEmailChanged("ru", "new@example.com"))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler(
            _composer.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(fact, CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "old@example.com"
                    && message.Subject == "the composed subject"
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
