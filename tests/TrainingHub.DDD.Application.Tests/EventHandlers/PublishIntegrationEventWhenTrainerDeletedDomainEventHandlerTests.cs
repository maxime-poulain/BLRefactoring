using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behavior covered for the last publisher: the fact a trainer's deletion leaves behind (ADR 0085).
/// </summary>
/// <remarks>
/// What the assertions pin is the flattening of the one attribute that matters: the portrait's
/// identity crosses the seam as a bare <see cref="Guid"/> or as nothing, because the consumer
/// that reads it back has no table left to resolve anything against.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerDeletedDomainEventHandlerTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();

    /// <summary>
    /// Handle, a trainer with a portrait, publishes both identities.
    /// </summary>
    [Fact]
    public async Task Handle_ATrainerWithAPortrait_PublishesBothIdentities()
    {
        var trainerId = TrainerId.Generate();
        var photoId = PhotoId.Generate();
        var domainEvent = new TrainerDeletedDomainEvent(trainerId, photoId);

        var sut = new PublishIntegrationEventWhenTrainerDeletedDomainEventHandler(_publisher.Object);
        await sut.Handle(domainEvent, CancellationToken.None);

        _publisher.Verify(publisher => publisher.PublishAsync(
                new TrainerDeletedIntegrationEvent(trainerId.Value, photoId.Value),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a trainer without one, publishes no photo to collect.
    /// </summary>
    [Fact]
    public async Task Handle_ATrainerWithoutOne_PublishesNoPhotoToCollect()
    {
        var trainerId = TrainerId.Generate();
        var domainEvent = new TrainerDeletedDomainEvent(trainerId, PhotoId: null);

        var sut = new PublishIntegrationEventWhenTrainerDeletedDomainEventHandler(_publisher.Object);
        await sut.Handle(domainEvent, CancellationToken.None);

        _publisher.Verify(publisher => publisher.PublishAsync(
                new TrainerDeletedIntegrationEvent(trainerId.Value, null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
