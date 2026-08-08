using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Writes an audit trail entry when the administration lifts the interdiction on a training.
/// </summary>
/// <remarks>
/// This one is journalisation for good, not merely for now, and the difference is worth stating.
/// Withholding has consumers waiting — the owner to notify, the index entry to drop — while
/// releasing has none: the training lands on <c>Unpublished</c>, where it was not indexed and where
/// nobody was listening. That is ADR 0050's own test, <em>nothing produces it, nothing consumes
/// it</em>, applied again rather than forgotten (ADR 0052).
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so what this handler writes
/// joins the same transaction as the change that raised the event.
/// </para>
/// </remarks>
public sealed class AuditWhenTrainingReleasedEventHandler(
    ILogger<AuditWhenTrainingReleasedEventHandler> logger)
    : IDomainEventHandler<TrainingReleasedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public ValueTask Handle(TrainingReleasedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Training {TrainingId} of trainer {TrainerId} was released; it is unpublished, and "
            + "publishing it is its owner's decision again.",
            notification.TrainingId.Value,
            notification.TrainerId.Value);

        return ValueTask.CompletedTask;
    }
}
