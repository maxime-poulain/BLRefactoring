using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;

/// <summary>
/// Checks <see cref="GetTrainingByIdQuery"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behaviour, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
public sealed class GetTrainingByIdQueryValidator : AbstractValidator<GetTrainingByIdQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetTrainingByIdQueryValidator()
    {
        RuleFor(training => training.Id).NotEmpty();
    }
}
