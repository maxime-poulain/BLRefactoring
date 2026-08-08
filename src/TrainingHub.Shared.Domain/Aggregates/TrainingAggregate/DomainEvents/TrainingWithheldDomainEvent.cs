using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Training"/> has been taken out of public view by the administration.
/// </summary>
/// <remarks>
/// Distinct from <see cref="TrainingUnpublishedDomainEvent"/> because the acts differ in what they
/// permit, not merely in who performed them: the owner may publish what they withdrew and may not
/// publish what was taken from them (ADR 0052).
/// <para>
/// It carries the reason, which the owner has to be told. The reason is also written on the
/// aggregate — the outbox is swept on a retention period (ADR 0033), so a fact that has been
/// delivered and swept cannot answer "why is my training unavailable" later.
/// </para>
/// <para>
/// It carries the title for the same audience and a narrower purpose: the notice sent to the owner
/// has to say which training was taken down, and an owner with a dozen of them told that "a
/// training" was withheld has been told nothing (ADR 0056).
/// </para>
/// </remarks>
/// <param name="TrainingId">The identifier of the withheld training.</param>
/// <param name="TrainerId">The identifier of the trainer owning the training.</param>
/// <param name="Title">The training's title, as it stood when it was withheld.</param>
/// <param name="Reason">Why it was withheld.</param>
public sealed record TrainingWithheldDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId,
    TrainingTitle Title,
    WithholdingReason Reason) : IDomainEvent;
