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
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, tells the account's own address, and names the way back in.
    /// </summary>
    [Fact]
    public async Task Handle_TellsTheAccountsOwnAddress_AndNamesTheWayBackIn()
    {
        // Arrange
        var fact = new PasswordChangedIntegrationEvent(
            Guid.NewGuid(), "grace.hopper@example.org", "grace.hopper");

        var sut = new SendPasswordChangedNoticeWhenPasswordChangedIntegrationEventHandler(_emailSender.Object);

        // Act
        await sut.HandleAsync(fact, CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "grace.hopper@example.org"
                    && message.Subject == "Your TrainingHub password was changed"
                    && message.Body.Contains("grace.hopper")
                    && message.Body.Contains("Forgot your password?")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
