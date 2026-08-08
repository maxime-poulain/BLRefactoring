using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Writes an audit trail entry when the sanction against a trainer is lifted.
/// </summary>
/// <remarks>
/// The other half of the trail: a log that records suspensions and not their lifting says a trainer
/// is still under sanction long after they are not. The lifting also travels outward, as
/// <c>TrainerReinstatedIntegrationEvent</c> — the trainer is told and their catalogue returns to
/// public view (ADR 0056) — and this line answers a different reader, the same way
/// <see cref="AuditWhenTrainerSuspendedEventHandler"/> does.
/// </remarks>
public sealed class AuditWhenTrainerReinstatedEventHandler(
    ILogger<AuditWhenTrainerReinstatedEventHandler> logger)
    : IDomainEventHandler<TrainerReinstatedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public ValueTask Handle(TrainerReinstatedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Trainer {TrainerId} was reinstated; their catalogue returns to public view exactly as they left it.",
            notification.TrainerId.Value);

        return ValueTask.CompletedTask;
    }
}
