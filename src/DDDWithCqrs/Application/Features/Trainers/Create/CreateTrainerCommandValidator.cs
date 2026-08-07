using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Create;

/// <summary>
/// Stands between <see cref="CreateTrainerCommand"/> and its handler, and asks about its identifiers.
/// </summary>
/// <remarks>
/// Everything about the trainer's <em>content</em> is checked by somebody better placed: the contract
/// declares shape and presence at model binding, and the domain judges what the values mean and
/// answers with its own codes. That is ADR 0043, and it is why the rules on the name and the address
/// went.
/// <para>
/// The identifiers are the exception, and this validator was emptied of them by mistake. ADR 0043
/// justified leaving it bare by saying that what remains for this layer is "an empty identifier that
/// would reach <c>EntityId.Create</c> and throw — and this command carries none". This command
/// carries two. <c>TrainerId</c> defaults to a fresh <c>Guid</c> but is <c>init</c>, so a caller can
/// set it back to <c>Guid.Empty</c>; <c>UserId</c> has no default at all and is handed straight to
/// <c>UserId.Create</c>. Both would throw rather than be refused. The rule that found this is
/// <c>EveryIdentifierAMessageCarries_IsRefusedEmptyByItsValidator</c> (ADR 0046) — a decision written
/// as prose would not have caught a record's own sentence being wrong.
/// </para>
/// </remarks>
public sealed class CreateTrainerCommandValidator : AbstractValidator<CreateTrainerCommand>
{
    /// <summary>
    /// Declares the rules that are this layer's to make.
    /// </summary>
    public CreateTrainerCommandValidator()
    {
        RuleFor(command => command.TrainerId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}
