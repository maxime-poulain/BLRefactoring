using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Edit;

/// <summary>
/// Stands between <see cref="EditTrainingCommand"/> and its handler, and asks one thing.
/// </summary>
/// <remarks>
/// The identifier, and nothing else. The contract declares the shape of every field at model
/// binding, before this pipeline runs, and the domain judges what those fields mean and answers
/// with its own codes — so the rules this validator used to carry on <c>Title</c> and
/// <c>Topics</c> were either dead or a second, stricter opinion only one host held.
/// <para>
/// What is left is the one refusal neither of the other two layers can make politely:
/// <c>Guid.Empty</c> is a perfectly well-formed <c>Guid</c>, so the contract has no reason to
/// reject it, and by the time the domain sees it <c>EntityId.Create</c> has already thrown — a 500
/// where the caller deserves a 400. See ADR 0043.
/// </para>
/// </remarks>
public sealed class EditTrainingCommandValidator : AbstractValidator<EditTrainingCommand>
{
    /// <summary>
    /// Declares the one rule that is this layer's to make.
    /// </summary>
    public EditTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId)
            .NotEmpty();
    }
}
