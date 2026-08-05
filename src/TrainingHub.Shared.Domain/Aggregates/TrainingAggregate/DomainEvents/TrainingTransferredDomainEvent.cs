using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Training"/> has changed hands (ADR 0036).
/// </summary>
/// <remarks>
/// Carries both owners on purpose: by the time a reaction runs, the aggregate has long forgotten
/// who used to hold the training — the same reason the contact-email fact carries the old
/// address.
/// </remarks>
/// <param name="TrainingId">The identifier of the transferred training.</param>
/// <param name="FormerTrainerId">The trainer who handed the training over.</param>
/// <param name="NewTrainerId">The trainer who received it.</param>
public sealed record TrainingTransferredDomainEvent(
    TrainingId TrainingId,
    TrainerId FormerTrainerId,
    TrainerId NewTrainerId) : IDomainEvent;
