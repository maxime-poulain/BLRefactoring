using Moq;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the farewell: the notice an erased account receives (ADR 0085).
/// </summary>
/// <remarks>
/// The one notice whose recipient is gone by the time it is delivered, which is why both the
/// address and the language ride on the fact: there is nothing left to read them from (ADR 0091).
/// </remarks>
public sealed class SendErasureNoticeWhenAccountErasedIntegrationEventHandlerTests
{
    private readonly Mock<INotificationComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, tells the erased address, in the language the account read.
    /// </summary>
    [Fact]
    public async Task Handle_TellsTheErasedAddress_InTheLanguageTheAccountRead()
    {
        // Arrange
        var fact = new AccountErasedIntegrationEvent(
            Guid.NewGuid(), "grace.hopper@example.org", "grace.hopper", "fr");

        _composer
            .Setup(composer => composer.AccountErased("fr", "grace.hopper"))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new SendErasureNoticeWhenAccountErasedIntegrationEventHandler(
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
