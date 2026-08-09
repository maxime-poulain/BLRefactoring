using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Outbox.Requeue;

/// <summary>
/// Checks <see cref="RequeueOutboxMessageCommand"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The identifier is refused empty here as well as at the boundary, and the two are not duplicates:
/// <c>[NotEmptyIdentifier]</c> answers a caller arriving over HTTP, this answers anything that
/// reaches <c>ICommandDispatcher</c> (ADR 0046). The one caller today is a controller; the argument
/// is that the application layer never assumes the boundary checked first, and it does not become
/// weaker because the message is about a table rather than an aggregate.
/// </remarks>
public sealed class RequeueOutboxMessageCommandValidator : AbstractValidator<RequeueOutboxMessageCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public RequeueOutboxMessageCommandValidator()
    {
        RuleFor(command => command.MessageId).NotEmpty();
    }
}
