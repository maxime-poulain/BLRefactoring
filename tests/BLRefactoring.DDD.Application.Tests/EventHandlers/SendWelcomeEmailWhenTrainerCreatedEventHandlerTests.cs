using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.EventHandlers;

public sealed class SendWelcomeEmailWhenTrainerCreatedEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    private SendWelcomeEmailWhenTrainerCreatedEventHandler CreateSut() =>
        new(_emailSender.Object);

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
