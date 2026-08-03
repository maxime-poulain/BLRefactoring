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
/// Behaviour covered for <c>NotifyPreviousAddressWhenTrainerContactEmailChangedEventHandler</c>.
/// </summary>
public sealed class NotifyPreviousAddressWhenTrainerContactEmailChangedEventHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    private NotifyPreviousAddressWhenTrainerContactEmailChangedEventHandler CreateSut() =>
        new(_emailSender.Object);

    /// <summary>
    /// Handle, notifies the previous address.
    /// </summary>
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
