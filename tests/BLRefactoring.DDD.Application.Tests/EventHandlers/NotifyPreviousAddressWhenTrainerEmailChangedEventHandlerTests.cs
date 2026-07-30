using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.EventHandlers;

public class NotifyPreviousAddressWhenTrainerEmailChangedEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    private NotifyPreviousAddressWhenTrainerEmailChangedEventHandler CreateSut() =>
        new(_emailSender.Object);

    [Fact]
    public async Task Handle_NotifiesThePreviousAddress()
    {
        var domainEvent = new TrainerEmailChangedDomainEvent(
            TrainerId.Generate(), "old@example.com", "new@example.com");

        var sut = CreateSut();
        await sut.Handle(domainEvent, CancellationToken.None);

        // The warning goes to the OLD address — the only one the legitimate
        // owner is guaranteed to still control — and names the new one.
        _emailSender.Verify(s => s.SendAsync(
                It.Is<EmailMessage>(m =>
                    m.Recipient == "old@example.com"
                    && m.Body.Contains("new@example.com")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
