using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/> has been created.
/// </summary>
/// <remarks>
/// The event carries the facts its consumers need (name and contact address for a
/// welcome message) rather than just the identifier: domain events are dispatched
/// before the aggregate is persisted, so a handler cannot load a freshly created
/// trainer from a repository — and an event should describe what happened on its
/// own anyway. Those facts are carried as the value objects the domain speaks in,
/// not as loose strings.
/// </remarks>
/// <param name="TrainerId">The identifier of the created trainer.</param>
/// <param name="Name">The name of the created trainer.</param>
/// <param name="ContactEmail">The contact address of the created trainer.</param>
public sealed record TrainerCreatedDomainEvent(
    TrainerId TrainerId,
    Name Name,
    Email ContactEmail) : IDomainEvent;
