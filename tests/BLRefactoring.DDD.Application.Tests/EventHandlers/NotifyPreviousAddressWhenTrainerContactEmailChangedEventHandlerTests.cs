using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.EventHandlers;

public sealed class NotifyPreviousAddressWhenTrainerContactEmailChangedEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    private NotifyPreviousAddressWhenTrainerContactEmailChangedEventHandler CreateSut() =>
        new(_emailSender.Object);

    [Fact]
    public async Task Handle_NotifiesThePreviousAddress()
    {
        var domainEvent = new TrainerContactEmailChangedDomainEvent(
            TrainerId.Generate(),
            Email.Create("old@example.com").ShouldBeSuccess(),
            Email.Create("new@example.com").ShouldBeSuccess());

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
