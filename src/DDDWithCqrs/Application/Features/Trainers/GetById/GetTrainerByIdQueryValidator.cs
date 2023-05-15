using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;

public class GetTrainerByIdQueryValidator : AbstractValidator<GetTrainerByIdQuery>
{
    public GetTrainerByIdQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
