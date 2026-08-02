using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;

public sealed class CreateTrainerCommandValidator : AbstractValidator<CreateTrainerCommand>
{
    public CreateTrainerCommandValidator()
    {
        RuleFor(command => command.ContactEmail).EmailAddress();
        RuleFor(command => command.Firstname).NotEmpty();
        RuleFor(command => command.Lastname).NotEmpty();
    }
}
