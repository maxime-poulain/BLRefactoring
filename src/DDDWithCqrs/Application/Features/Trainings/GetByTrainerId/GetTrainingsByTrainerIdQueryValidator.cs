using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;

public sealed class GetTrainingsByTrainerIdQueryValidator : AbstractValidator<GetTrainingsByTrainerIdQuery>
{
    public GetTrainingsByTrainerIdQueryValidator()
    {
        RuleFor(query => query.TrainerId).NotEmpty();
    }
}
