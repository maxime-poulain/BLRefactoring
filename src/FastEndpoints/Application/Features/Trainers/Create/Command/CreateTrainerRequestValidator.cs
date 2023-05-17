using FluentValidation;

namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Command;

/// <summary>
/// Represents a validator for the <see cref="CreateTrainerCommand"/>.
/// </summary>
public class CreateTrainerRequestValidator : AbstractValidator<CreateTrainerCommand>
{
    public CreateTrainerRequestValidator()
    {
        RuleFor(command => command.Firstname)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(5);

        RuleFor(command => command.Lastname)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(5);

        RuleFor(command => command.Email).EmailAddress();
    }
}
