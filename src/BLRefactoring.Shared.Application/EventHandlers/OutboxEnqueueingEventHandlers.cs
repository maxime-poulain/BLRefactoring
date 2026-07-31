using BLRefactoring.Shared.Application.IntegrationEvents;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace BLRefactoring.Shared.Application.EventHandlers;

/// <summary>
/// The translators between the domain's internal events and the facts this system publishes
/// outside its transaction.
/// </summary>
/// <remarks>
/// <para>
/// These used to perform the side effect directly — send the email, refresh the index. Running
/// inside <c>SavingChangesAsync</c>, that meant the email left before the commit: a transaction
/// failing afterwards left a welcome message delivered for a trainer that never existed. They
/// now only enqueue, so the announcement is written by the same <c>SaveChanges</c> as the change
/// it announces and is published once that has actually committed.
/// </para>
/// <para>
/// This is also where value objects become primitives. It is the only place that knows both
/// vocabularies, which is precisely what makes it the anti-corruption seam.
/// </para>
/// </remarks>
public sealed class EnqueueTrainerRegisteredWhenTrainerCreatedEventHandler(
    IOutbox outbox,
    TimeProvider timeProvider)
    : IDomainEventHandler<TrainerCreatedDomainEvent>
{
    public ValueTask Handle(TrainerCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        outbox.Enqueue(new TrainerRegisteredIntegrationEvent(
            notification.TrainerId,
            notification.Name.Firstname,
            notification.Name.Lastname,
            notification.ContactEmail.FullAddress,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc cref="EnqueueTrainerRegisteredWhenTrainerCreatedEventHandler"/>
public sealed class EnqueueContactEmailChangedWhenTrainerContactEmailChangedEventHandler(
    IOutbox outbox,
    TimeProvider timeProvider)
    : IDomainEventHandler<TrainerContactEmailChangedDomainEvent>
{
    public ValueTask Handle(
        TrainerContactEmailChangedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        outbox.Enqueue(new TrainerContactEmailChangedIntegrationEvent(
            notification.TrainerId,
            notification.OldContactEmail.FullAddress,
            notification.NewContactEmail.FullAddress,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc cref="EnqueueTrainerRegisteredWhenTrainerCreatedEventHandler"/>
public sealed class EnqueueTrainingPublishedWhenTrainingCreatedEventHandler(
    IOutbox outbox,
    TimeProvider timeProvider)
    : IDomainEventHandler<TrainingCreatedDomainEvent>
{
    public ValueTask Handle(TrainingCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        outbox.Enqueue(new TrainingPublishedIntegrationEvent(
            notification.TrainingId,
            notification.TrainerId,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc cref="EnqueueTrainerRegisteredWhenTrainerCreatedEventHandler"/>
public sealed class EnqueueTrainingRevisedWhenTrainingEditedEventHandler(
    IOutbox outbox,
    TimeProvider timeProvider)
    : IDomainEventHandler<TrainingEditedDomainEvent>
{
    public ValueTask Handle(TrainingEditedDomainEvent notification, CancellationToken cancellationToken)
    {
        outbox.Enqueue(new TrainingRevisedIntegrationEvent(
            notification.TrainingId,
            notification.TrainerId,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}
