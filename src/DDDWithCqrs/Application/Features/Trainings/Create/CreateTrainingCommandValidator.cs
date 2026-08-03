using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Create;

/// <summary>
/// Checks <see cref="CreateTrainingCommand"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behaviour, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
public sealed class CreateTrainingCommandValidator : AbstractValidator<CreateTrainingCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public CreateTrainingCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.Topics)
            .NotEmpty();
    }
}
