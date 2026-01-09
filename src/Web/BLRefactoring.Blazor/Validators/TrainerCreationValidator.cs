using BLRefactoring.GeneratedClients;
using FluentValidation;

namespace BLRefactoring.Blazor.Validators;

public class TrainerCreationValidator : AbstractValidator<TrainerCreationRequest>
{
    public TrainerCreationValidator()
    {
        RuleFor(x => x.Firstname)
            .NotEmpty().WithMessage("Le prénom est requis")
            .MaximumLength(50).WithMessage("Le prénom ne peut pas dépasser 50 caractères")
            .Must(BeValidName).WithMessage("Le prénom ne peut contenir que des lettres, espaces et tirets");

        RuleFor(x => x.Lastname)
            .NotEmpty().WithMessage("Le nom de famille est requis")
            .MaximumLength(50).WithMessage("Le nom de famille ne peut pas dépasser 50 caractères")
            .Must(BeValidName).WithMessage("Le nom ne peut contenir que des lettres, espaces et tirets");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("L'email est requis")
            .EmailAddress().WithMessage("Format d'email invalide")
            .MaximumLength(255).WithMessage("L'email ne peut pas dépasser 255 caractères");
    }

    private bool BeValidName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               name.All(c => char.IsLetter(c) || c == ' ' || c == '-');
    }

    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var result = await ValidateAsync(ValidationContext<TrainerCreationRequest>.CreateWithOptions((TrainerCreationRequest)model, x => x.IncludeProperties(propertyName)));
        if (result.IsValid)
            return Array.Empty<string>();
        return result.Errors.Select(e => e.ErrorMessage);
    };
}
