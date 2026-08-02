using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;

public sealed class EditTrainingCommandValidator : AbstractValidator<EditTrainingCommand>
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
