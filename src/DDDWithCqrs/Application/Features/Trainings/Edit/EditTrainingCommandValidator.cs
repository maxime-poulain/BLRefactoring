using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Edit;

/// <summary>
/// Checks <see cref="EditTrainingCommand"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behaviour, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
public sealed class EditTrainingCommandValidator : AbstractValidator<EditTrainingCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public EditTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId)
            .NotEmpty();

        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.Topics)
            .NotEmpty();
    }
}
