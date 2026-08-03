using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Delete;

/// <summary>
/// Checks <see cref="DeleteTrainingCommand"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behaviour, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
public sealed class DeleteTrainingCommandValidator : AbstractValidator<DeleteTrainingCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public DeleteTrainingCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
