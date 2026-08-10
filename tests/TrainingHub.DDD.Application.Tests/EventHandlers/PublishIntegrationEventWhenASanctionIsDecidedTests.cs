using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behavior covered for the three publishers ADR 0056 adds.
/// </summary>
/// <remarks>
/// Each of them flattens a domain event into the fact that leaves the context, and what the
/// assertions pin is the flattening: a reason and a title are value objects on one side of this
/// seam and strings on the other, and a consumer deserializing the message has no way of building
/// either back.
/// </remarks>
public sealed class PublishIntegrationEventWhenASanctionIsDecidedTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();

    /// <summary>
    /// Handle, a suspension, publishes the trainer and the reason as a string.
    /// </summary>
    [Fact]
    public async Task Handle_ASuspension_PublishesTheTrainerAndTheReasonAsAString()
    {
        var trainerId = TrainerId.Generate();
        var domainEvent = new TrainerSuspendedDomainEvent(
            trainerId,
            SuspensionReason.Create("Repeated breaches of the content policy.").ShouldBeSuccess());

        var sut = new PublishIntegrationEventWhenTrainerSuspendedEventHandler(_publisher.Object);
        await sut.Handle(domainEvent, CancellationToken.None);

        _publisher.Verify(publisher => publisher.PublishAsync(
                new TrainerSuspendedIntegrationEvent(
                    trainerId.Value, "Repeated breaches of the content policy."),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a reinstatement, publishes the trainer and nothing else.
    /// </summary>
    /// <remarks>
    /// A lifting restores rather than decides, so there is no motive to carry — asserted, because
    /// the temptation to echo the reason the sanction carried is what would make the fact say why
    /// it was lifted when nobody recorded that.
    /// </remarks>
    [Fact]
    public async Task Handle_AReinstatement_PublishesTheTrainerAndNothingElse()
    {
        var trainerId = TrainerId.Generate();

        var sut = new PublishIntegrationEventWhenTrainerReinstatedEventHandler(_publisher.Object);
        await sut.Handle(new TrainerReinstatedDomainEvent(trainerId), CancellationToken.None);

        _publisher.Verify(publisher => publisher.PublishAsync(
                new TrainerReinstatedIntegrationEvent(trainerId.Value),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a withholding, publishes the title beside the reason.
    /// </summary>
    /// <remarks>
    /// The title is what lets the notice name the training to its owner. Without it the fact is
    /// well-formed and the email it produces is useless.
    /// </remarks>
    [Fact]
    public async Task Handle_AWithholding_PublishesTheTitleBesideTheReason()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();

        var domainEvent = new TrainingWithheldDomainEvent(
            trainingId,
            trainerId,
            TrainingTitle.Create("Advanced domain modeling").ShouldBeSuccess(),
            WithholdingReason.Create("Reported for misleading claims.").ShouldBeSuccess());

        var sut = new PublishIntegrationEventWhenTrainingWithheldEventHandler(_publisher.Object);
        await sut.Handle(domainEvent, CancellationToken.None);

        _publisher.Verify(publisher => publisher.PublishAsync(
                new TrainingWithheldIntegrationEvent(
                    trainingId.Value,
                    trainerId.Value,
                    "Advanced domain modeling",
                    "Reported for misleading claims."),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
