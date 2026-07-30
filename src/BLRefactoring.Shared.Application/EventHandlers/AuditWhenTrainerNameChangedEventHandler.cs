using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using Microsoft.Extensions.Logging;

namespace BLRefactoring.Shared.Application.EventHandlers;

/// <summary>
/// Writes an audit trail entry when a trainer's name changes.
/// </summary>
/// <remarks>
/// Illustrates journalisation as a use of a domain event: identity-related
/// changes are business facts worth tracing, and the event carries both the old
/// and the new value, so the audit entry is complete without loading anything.
/// A real system might append to a dedicated audit store; structured logging is
/// enough to demonstrate the pattern.
/// </remarks>
public sealed class AuditWhenTrainerNameChangedEventHandler(
    ILogger<AuditWhenTrainerNameChangedEventHandler> logger)
    : IDomainEventHandler<TrainerNameChangedDomainEvent>
{
    public ValueTask Handle(TrainerNameChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Trainer {TrainerId} was renamed from `{OldFirstname} {OldLastname}` to `{NewFirstname} {NewLastname}`.",
            notification.TrainerId.Value,
            notification.OldFirstname,
            notification.OldLastname,
            notification.NewFirstname,
            notification.NewLastname);

        return ValueTask.CompletedTask;
    }
}
