using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Writes an audit trail entry when a trainer is placed under sanction.
/// </summary>
/// <remarks>
/// Journalisation, beside the announcement rather than instead of it. The sanction now leaves the
/// context as an integration event too (ADR 0056), and the two answer different readers: the fact
/// tells the trainer and the index what changed, this line tells whoever reads the logs of a
/// running system, where every other identity-affecting change is written down —
/// beside <see cref="AuditWhenTrainerNameChangedEventHandler"/>.
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
        // The reason, because it is the one part of the decision no log could recover on its own:
        // who acted and when are already stamped on this line by ADR 0027's enricher, and the
        // motive lived only in the administrator's head until ADR 0052 gave it a value object.
        logger.LogInformation(
            "Trainer {TrainerId} was suspended for '{Reason}'; their catalogue leaves public view "
            + "until the sanction is lifted.",
            notification.TrainerId.Value,
            notification.Reason.Value);

        return ValueTask.CompletedTask;
    }
}
