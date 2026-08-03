using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetById;

/// <summary>
/// Checks <see cref="GetTrainerByIdQuery"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behaviour, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
public sealed class GetTrainerByIdQueryValidator : AbstractValidator<GetTrainerByIdQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetTrainerByIdQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
