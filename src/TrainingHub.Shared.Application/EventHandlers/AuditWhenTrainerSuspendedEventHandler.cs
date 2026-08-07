using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Writes an audit trail entry when a trainer is placed under sanction.
/// </summary>
/// <remarks>
/// Journalisation rather than announcement, and deliberately so: ADR 0050 gives the suspension no
/// integration event, because no surface raises it and no context consumes it, and building outbox
/// plumbing for a fact nobody produces is anticipation. A sanction that left no trace at all would
/// be worse than one that does, though — so the fact is written down where every other
/// identity-affecting change is, beside
/// <see cref="AuditWhenTrainerNameChangedEventHandler"/>.
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so what this handler writes
/// joins the same transaction as the change that raised the event.
/// </para>
/// </remarks>
public sealed class AuditWhenTrainerSuspendedEventHandler(
    ILogger<AuditWhenTrainerSuspendedEventHandler> logger)
    : IDomainEventHandler<TrainerSuspendedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public ValueTask Handle(TrainerSuspendedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Trainer {TrainerId} was suspended; their catalogue leaves public view until the sanction is lifted.",
            notification.TrainerId.Value);

        return ValueTask.CompletedTask;
    }
}
