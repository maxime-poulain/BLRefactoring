using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;

public class GetTrainingByIdQueryValidator : AbstractValidator<GetTrainingByIdQuery>
{
    public GetTrainingByIdQueryValidator()
    {
        RuleFor(training => training.Id).NotEmpty();
    }
}
