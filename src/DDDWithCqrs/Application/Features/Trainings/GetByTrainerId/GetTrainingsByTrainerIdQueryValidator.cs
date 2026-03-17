using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;

public class GetTrainingsByTrainerIdQueryValidator : AbstractValidator<GetTrainingsByTrainerIdQuery>
{
    public GetTrainingsByTrainerIdQueryValidator()
    {
        RuleFor(query => query.TrainerId).NotEmpty();
    }
}
