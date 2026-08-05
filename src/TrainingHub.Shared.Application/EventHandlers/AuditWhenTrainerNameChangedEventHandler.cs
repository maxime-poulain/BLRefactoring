using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Writes an audit trail entry when a trainer's name changes.
/// </summary>
/// <remarks>
/// Illustrates journalisation as a use of a domain event: identity-related
/// changes are business facts worth tracing, and the event carries both the old
/// and the new value, so the audit entry is complete without loading anything.
/// A real system might append to a dedicated audit store; structured logging is
/// enough to demonstrate the pattern.
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so anything this handler
/// writes joins the same transaction as the change that raised the event.
/// </para>
/// </remarks>
public sealed class AuditWhenTrainerNameChangedEventHandler(
    ILogger<AuditWhenTrainerNameChangedEventHandler> logger)
    : IDomainEventHandler<TrainerNameChangedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public ValueTask Handle(TrainerNameChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Trainer {TrainerId} was renamed from `{OldFirstname} {OldLastname}` to `{NewFirstname} {NewLastname}`.",
            notification.TrainerId.Value,
            notification.OldName.Firstname,
            notification.OldName.Lastname,
            notification.NewName.Firstname,
            notification.NewName.Lastname);

        return ValueTask.CompletedTask;
    }
}
