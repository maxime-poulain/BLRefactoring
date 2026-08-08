using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Writes an audit trail entry when a training is withheld by the administration.
/// </summary>
/// <remarks>
/// Journalisation rather than announcement, for now and for the reason
/// <see cref="AuditWhenTrainerSuspendedEventHandler"/> gives: no surface raises the decision yet, so
/// there is nothing to carry outward and outbox plumbing for a fact nobody produces is
/// anticipation. Withholding <em>will</em> earn an integration event — the owner has to be told and
/// the search index has to drop the entry — and it earns it in the commit that gives it a use case.
/// <para>
/// The reason is on the line, because it is what a reader of the audit trail would otherwise have to
/// go and fetch from the aggregate. Who acted and when are stamped by ADR 0027's enricher.
/// </para>
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so what this handler writes
/// joins the same transaction as the change that raised the event.
/// </para>
/// </remarks>
public sealed class AuditWhenTrainingWithheldEventHandler(
    ILogger<AuditWhenTrainingWithheldEventHandler> logger)
    : IDomainEventHandler<TrainingWithheldDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public ValueTask Handle(TrainingWithheldDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Training {TrainingId} of trainer {TrainerId} was withheld for '{Reason}'; its owner "
            + "cannot publish it again.",
            notification.TrainingId.Value,
            notification.TrainerId.Value,
            notification.Reason.Value);

        return ValueTask.CompletedTask;
    }
}
