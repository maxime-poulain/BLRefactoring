using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces an administrative removal outside the bounded context: translates the domain event
/// into <see cref="TrainingWithheldIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// Withholding earns a fact and releasing does not, which is ADR 0052's test applied rather than
/// forgotten: this one has consumers waiting — the notice to the owner and the index entry to drop
/// — and a release lands on a state where the training was not indexed and nobody was listening.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainingWithheldEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainingWithheldDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingWithheldDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainingWithheldIntegrationEvent(
                notification.TrainingId.Value,
                notification.TrainerId.Value,
                notification.Title.Value,
                notification.Reason.Value),
            cancellationToken);
}
