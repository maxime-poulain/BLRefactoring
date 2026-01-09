using BLRefactoring.GeneratedClients;
using FluentValidation;

namespace BLRefactoring.Blazor.Validators;

public class LoginRequestValidator : ValidatorBase<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username is required");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(4)
            .WithMessage("Password must be at least 4 characters long");
    }
}
