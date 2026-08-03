using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/>'s contact email has changed.
/// </summary>
/// <remarks>
/// This is the business address the trainer publishes, not the credential of their
/// identity account: changing it has no effect whatsoever on how they sign in.
/// </remarks>
/// <param name="TrainerId">The identifier of the trainer whose contact email changed.</param>
/// <param name="OldContactEmail">The contact address before the change.</param>
/// <param name="NewContactEmail">The contact address after the change.</param>
public sealed record TrainerContactEmailChangedDomainEvent(
    TrainerId TrainerId,
    Email OldContactEmail,
    Email NewContactEmail) : IDomainEvent;
