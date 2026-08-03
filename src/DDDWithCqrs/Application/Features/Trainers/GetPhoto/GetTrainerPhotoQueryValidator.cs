using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetPhoto;

/// <summary>
/// Validates <see cref="GetTrainerPhotoQuery"/>.
/// </summary>
public sealed class GetTrainerPhotoQueryValidator : AbstractValidator<GetTrainerPhotoQuery>
{
    /// <summary>
    /// Refuses an empty identifier before it reaches the database.
    /// </summary>
    public GetTrainerPhotoQueryValidator()
    {
        RuleFor(query => query.TrainerId).NotEmpty();
    }
}
