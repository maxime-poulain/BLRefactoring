using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behavior covered for <c>PublishIntegrationEventWhenTrainerContactEmailChangedDomainEventHandler</c>.
/// </summary>
/// <remarks>
/// The order of the two addresses is the whole point of this fact — the warning goes to the old
/// one — so the assertion pins each to its property rather than checking both are merely present.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerContactEmailChangedDomainEventHandlerTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();
    private readonly Mock<IAccountLanguageQuery> _languages = new();

    /// <summary>
    /// Handle, publishes both addresses, each in its place.
    /// </summary>
    [Fact]
    public async Task Handle_PublishesBothAddresses_EachInItsPlace()
    {
        var trainerId = TrainerId.Generate();
        var domainEvent = new TrainerContactEmailChangedDomainEvent(
            trainerId,
            Email.Create("old@example.com").ShouldBeSuccess(),
            Email.Create("new@example.com").ShouldBeSuccess());

        _languages
            .Setup(languages => languages.ByTrainerIdAsync(trainerId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ru");

        var sut = new PublishIntegrationEventWhenTrainerContactEmailChangedDomainEventHandler(
            _publisher.Object, _languages.Object);
        await sut.Handle(domainEvent, CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(
                new TrainerContactEmailChangedIntegrationEvent(
                    trainerId.Value, "old@example.com", "new@example.com", "ru"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
