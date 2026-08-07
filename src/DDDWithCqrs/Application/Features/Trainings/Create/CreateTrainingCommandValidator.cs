using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Create;

/// <summary>
/// Stands between <see cref="CreateTrainingCommand"/> and its handler, and asks about its identifier.
/// </summary>
/// <remarks>
/// Everything about the training's <em>content</em> is checked by somebody better placed: the
/// contract declares shape and presence at model binding, before this pipeline runs, and the domain
/// judges what the values mean and answers with its own codes. That is ADR 0043, and it is why the
/// rules on the title and the topics went.
/// <para>
/// The identifier is the exception, and it was emptied out by mistake. ADR 0043 justified leaving
/// this validator bare by saying that what remains for this layer is "an empty identifier that would
/// reach <c>EntityId.Create</c> and throw — and this command carries none". It carries one:
/// <c>TrainingId</c> defaults to a fresh <c>Guid</c>, but the property is <c>init</c>, so a caller
/// can set it back to <c>Guid.Empty</c>, and the handler hands it to <c>TrainingId.Create</c>
/// unexamined. Over HTTP nothing can: the contract has no such field, and the identifier is minted
/// here. A dispatcher's other callers are not so constrained (ADR 0046).
/// </para>
/// </remarks>
public sealed class CreateTrainingCommandValidator : AbstractValidator<CreateTrainingCommand>
{
    /// <summary>
    /// Declares the one rule that is this layer's to make.
    /// </summary>
    public CreateTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId).NotEmpty();
    }
}
