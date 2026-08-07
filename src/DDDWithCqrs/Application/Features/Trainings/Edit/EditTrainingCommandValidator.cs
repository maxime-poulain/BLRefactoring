using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Edit;

/// <summary>
/// Stands between <see cref="EditTrainingCommand"/> and its handler, and asks one thing.
/// </summary>
/// <remarks>
/// The identifier, and nothing else. The contract declares the shape of every field at model
/// binding, before this pipeline runs, and the domain judges what those fields mean and answers
/// with its own codes — so the rules this validator used to carry on <c>Title</c> and
/// <c>Topics</c> were either dead or a second, stricter opinion only one host held (ADR 0043).
/// <para>
/// The identifier is different, and stays. <c>[NotEmptyIdentifier]</c> on the route parameter now
/// refuses <c>Guid.Empty</c> at model binding, which closes the HTTP path on both hosts — but it
/// closes only that path. This rule answers for every other way a command can reach
/// <c>ICommandDispatcher</c>: an integration event consumer, a background service, a scheduler.
/// None exists today; the point is that the day one does, the guard is already there rather than
/// silently absent, and the failure is a named refusal instead of an exception out of
/// <c>EntityId.Create</c> (ADR 0046).
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
