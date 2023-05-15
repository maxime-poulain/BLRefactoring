using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete;

public class DeleteTrainerCommandValidator : AbstractValidator<DeleteTrainerCommand>
{
    public DeleteTrainerCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("A trainer id is required to proceed to its deletion.");
    }
}
