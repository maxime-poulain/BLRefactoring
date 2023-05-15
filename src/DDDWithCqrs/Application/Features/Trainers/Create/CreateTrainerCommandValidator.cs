using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;

public class CreateTrainerCommandValidator : AbstractValidator<CreateTrainerCommand>
{
    public CreateTrainerCommandValidator()
    {
        RuleFor(command => command.Email).EmailAddress();
        RuleFor(command => command.Firstname).NotEmpty();
        RuleFor(command => command.Lastname).NotEmpty();
    }
}
