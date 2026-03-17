using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;

public class DeleteTrainingCommandValidator : AbstractValidator<DeleteTrainingCommand>
{
    public DeleteTrainingCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
