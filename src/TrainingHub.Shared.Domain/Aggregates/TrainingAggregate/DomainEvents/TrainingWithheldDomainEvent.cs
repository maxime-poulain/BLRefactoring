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
/// </remarks>
/// <param name="TrainingId">The identifier of the withheld training.</param>
/// <param name="TrainerId">The identifier of the trainer owning the training.</param>
/// <param name="Reason">Why it was withheld.</param>
public sealed record TrainingWithheldDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId,
    WithholdingReason Reason) : IDomainEvent;
