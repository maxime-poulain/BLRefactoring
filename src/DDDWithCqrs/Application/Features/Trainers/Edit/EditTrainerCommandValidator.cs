using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;

public sealed class EditTrainerCommandValidator : AbstractValidator<EditTrainerCommand>
{
    public EditTrainerCommandValidator()
    {
        RuleFor(command => command.TrainerId).NotEmpty();
        RuleFor(command => command.ContactEmail).EmailAddress();
        RuleFor(command => command.Firstname).NotEmpty();
        RuleFor(command => command.Lastname).NotEmpty();
    }
}
