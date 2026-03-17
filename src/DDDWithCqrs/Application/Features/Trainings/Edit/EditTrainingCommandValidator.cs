using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;

public class EditTrainingCommandValidator : AbstractValidator<EditTrainingCommand>
{
    public EditTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId)
            .NotEmpty();

        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.Topics)
            .NotEmpty();
    }
}
