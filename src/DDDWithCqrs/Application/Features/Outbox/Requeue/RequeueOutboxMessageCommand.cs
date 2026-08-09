using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Outbox.Requeue;

/// <summary>
/// Asks that one poison message be handed back to the delivery worker (ADR 0061).
/// </summary>
public sealed class RequeueOutboxMessageCommand(Guid messageId) : ICommand<Result>
{
    /// <summary>
    /// The outbox message to requeue.
    /// </summary>
    public Guid MessageId { get; init; } = messageId;
}

/// <summary>
/// Runs <see cref="RequeueOutboxMessageCommand"/>.
/// </summary>
/// <remarks>
/// The one command in this stack that loads no aggregate and opens no unit of work, because there
/// is none to open: the decision — is this row poison, and what does putting it back mean — belongs
/// to the envelope, and the port is what reaches it. The handler answers a bare
/// <see cref="Result"/> like every other; what the message now looks like is read back with
/// <c>GetPoisonedMessagesQuery</c>.
/// </remarks>
public sealed class RequeueOutboxMessageCommandHandler(IOutboxOperations outboxOperations)
    : ICommandHandler<RequeueOutboxMessageCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(RequeueOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await outboxOperations.RequeueAsync(request.MessageId, cancellationToken);
    }
}
