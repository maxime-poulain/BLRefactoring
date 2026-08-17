using Moq;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the farewell: the notice an erased account receives (ADR 0085).
/// </summary>
public sealed class SendErasureNoticeWhenAccountErasedIntegrationEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, tells the erased address, and says the name is free again.
    /// </summary>
    [Fact]
    public async Task Handle_TellsTheErasedAddress_AndSaysTheNameIsFreeAgain()
    {
        // Arrange
        var fact = new AccountErasedIntegrationEvent(
            Guid.NewGuid(), "grace.hopper@example.org", "grace.hopper");

        var sut = new SendErasureNoticeWhenAccountErasedIntegrationEventHandler(_emailSender.Object);

        // Act
        await sut.HandleAsync(fact, CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "grace.hopper@example.org"
                    && message.Subject == "Your TrainingHub account was erased"
                    && message.Body.Contains("grace.hopper")
                    && message.Body.Contains("registering anew")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
