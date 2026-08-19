using Moq;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the owner's alarm bell: the notice a changed password sends (ADR 0084).
/// </summary>
public sealed class SendPasswordChangedNoticeWhenPasswordChangedIntegrationEventHandlerTests
{
    private readonly Mock<INotificationComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, tells the account's own address, in the language it read.
    /// </summary>
    [Fact]
    public async Task Handle_TellsTheAccountsOwnAddress_InTheLanguageItRead()
    {
        // Arrange
        var fact = new PasswordChangedIntegrationEvent(
            Guid.NewGuid(), "grace.hopper@example.org", "grace.hopper", "ru");

        _composer
            .Setup(composer => composer.PasswordChanged("ru", "grace.hopper"))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new SendPasswordChangedNoticeWhenPasswordChangedIntegrationEventHandler(
            _composer.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(fact, CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "grace.hopper@example.org"
                    && message.Subject == "the composed subject"
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
