using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behaviour covered for <c>SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler</c>.
/// </summary>
public sealed class SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, sends the welcome email to the created trainer.
    /// </summary>
    [Fact]
    public async Task Handle_SendsTheWelcomeEmail_ToTheCreatedTrainer()
    {
        var fact = new TrainerCreatedIntegrationEvent(
            Guid.NewGuid(), "John", "Doe", "john.doe@example.com");

        var sut = new SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler(_emailSender.Object);
        await sut.HandleAsync(fact, CancellationToken.None);

        _emailSender.Verify(s => s.SendAsync(
                It.Is<EmailMessage>(m =>
                    m.Recipient == "john.doe@example.com"
                    && m.Body.Contains("John")
                    && m.Body.Contains("Doe")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
