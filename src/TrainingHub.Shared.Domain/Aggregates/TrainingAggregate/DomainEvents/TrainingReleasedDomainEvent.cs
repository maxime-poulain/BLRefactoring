using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

/// <summary>
/// Raised when the administration has lifted the interdiction on a <see cref="Training"/>.
/// </summary>
/// <remarks>
/// The training lands on <see cref="ValueObjects.TrainingStatus.Unpublished"/>, never straight back
/// on offer: the administration lifts an interdiction, it does not decide to put a training back in
/// the window. Publishing is the owner's call again (ADR 0052).
/// <para>
/// No integration event follows this one, and that is the same test ADR 0050 applied to the two
/// suspension facts rather than an omission: the training returns to a state where it was not
/// indexed and where nobody was waiting, so nothing produces it outward and nothing consumes it.
/// </para>
/// </remarks>
/// <param name="TrainingId">The identifier of the released training.</param>
/// <param name="TrainerId">The identifier of the trainer owning the training.</param>
public sealed record TrainingReleasedDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId) : IDomainEvent;
