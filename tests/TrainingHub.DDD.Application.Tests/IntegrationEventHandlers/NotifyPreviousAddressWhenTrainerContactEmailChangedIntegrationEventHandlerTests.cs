using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for <c>NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler</c>.
/// </summary>
public sealed class NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, warns the old address, and names the new one.
    /// </summary>
    [Fact]
    public async Task Handle_WarnsTheOldAddress_AndNamesTheNewOne()
    {
        var fact = new TrainerContactEmailChangedIntegrationEvent(
            Guid.NewGuid(), "old@example.com", "new@example.com");

        var sut = new NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler(_emailSender.Object);
        await sut.HandleAsync(fact, CancellationToken.None);

        _emailSender.Verify(s => s.SendAsync(
                It.Is<EmailMessage>(m =>
                    m.Recipient == "old@example.com"
                    && m.Body.Contains("new@example.com")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
