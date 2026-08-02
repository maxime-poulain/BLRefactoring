using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;

public sealed class CreateTrainingCommandValidator : AbstractValidator<CreateTrainingCommand>
{
    public CreateTrainingCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.Topics)
            .NotEmpty();
    }
}
