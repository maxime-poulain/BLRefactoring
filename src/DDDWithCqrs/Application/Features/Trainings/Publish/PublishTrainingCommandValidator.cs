using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Publish;

/// <summary>
/// Checks <see cref="PublishTrainingCommand"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behavior, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
/// <remarks>
/// The rule is stated here as well as on the HTTP contract, and the two are not duplicates of one
/// another: they answer different entry points. <c>[NotEmptyIdentifier]</c> refuses the empty
/// identifier at model binding for a caller arriving over HTTP; this rule refuses it for anything
/// that reaches <c>ICommandDispatcher</c> or <c>IQueryDispatcher</c> — an integration event
/// consumer, a background service, a scheduler, a future entry point nobody has written yet. Only
/// controllers dispatch today, so today the second guard never fires; the day that changes, nothing
/// in the code would have announced its absence, and the failure would be an exception out of
/// <c>EntityId.Create</c> rather than a named refusal (ADR 0046).
/// <para>
/// This is what ADR 0043 means by validating where the rule lives: an identifier that must name
/// something is a precondition of the message, not of the transport that happened to carry it. The
/// application layer stays agnostic of the API layer, and does not assume a boundary it cannot see
/// has already checked its inputs.
/// </para>
/// </remarks>
public sealed class PublishTrainingCommandValidator : AbstractValidator<PublishTrainingCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public PublishTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId).NotEmpty();
    }
}
