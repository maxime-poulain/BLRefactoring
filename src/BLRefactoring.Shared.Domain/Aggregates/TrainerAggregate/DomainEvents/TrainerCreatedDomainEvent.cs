using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/> has been created.
/// </summary>
/// <remarks>
/// The event carries the facts its consumers need (name and contact address for a
/// welcome message) rather than just the identifier: domain events are dispatched
/// before the aggregate is persisted, so a handler cannot load a freshly created
/// trainer from a repository — and an event should describe what happened on its
/// own anyway.
/// </remarks>
/// <param name="TrainerId">The identifier of the created trainer.</param>
/// <param name="Firstname">The first name of the created trainer.</param>
/// <param name="Lastname">The last name of the created trainer.</param>
/// <param name="ContactEmail">The contact email address of the created trainer.</param>
public sealed record TrainerCreatedDomainEvent(
    TrainerId TrainerId,
    string Firstname,
    string Lastname,
    string ContactEmail) : IDomainEvent;
