using TrainingHub.Shared;
using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behaviour covered for <c>SendWelcomeEmailWhenTrainerCreatedEventHandler</c>.
/// </summary>
public sealed class SendWelcomeEmailWhenTrainerCreatedEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    private SendWelcomeEmailWhenTrainerCreatedEventHandler CreateSut() =>
        new(_emailSender.Object);

    /// <summary>
    /// Handle, sends welcome email to new trainer.
    /// </summary>
    [Fact]
    public async Task Handle_SendsWelcomeEmailToNewTrainer()
    {
        var domainEvent = new TrainerCreatedDomainEvent(
            TrainerId.Generate(),
            Name.Create("John", "Doe").ShouldBeSuccess(),
            Email.Create("john.doe@example.com").ShouldBeSuccess());

        var sut = CreateSut();
        await sut.Handle(domainEvent, CancellationToken.None);

        _emailSender.Verify(s => s.SendAsync(
                It.Is<EmailMessage>(m =>
                    m.Recipient == "john.doe@example.com"
                    && m.Body.Contains("John")
                    && m.Body.Contains("Doe")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
