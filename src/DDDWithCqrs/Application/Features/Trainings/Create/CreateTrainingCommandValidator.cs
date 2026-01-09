using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;

public class CreateTrainingCommandValidator : AbstractValidator<CreateTrainingCommand>
{
    public CreateTrainingCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.TrainingId)
            .NotEqual(Guid.Empty)
            .WithMessage("Invalid trainer identifier");

        RuleFor(command => command.Topics)
            .NotEmpty();
    }

}
