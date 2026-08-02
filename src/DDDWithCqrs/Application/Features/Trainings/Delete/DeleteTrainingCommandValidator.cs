using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;

public sealed class DeleteTrainingCommandValidator : AbstractValidator<DeleteTrainingCommand>
{
    public DeleteTrainingCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
