using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;

public class CreateTrainingCommandValidator : AbstractValidator<CreateTrainingCommand>
{
    public CreateTrainingCommandValidator()
    {
        RuleFor(command => command.StartDate)
            .GreaterThan(DateTime.Now.AddMinutes(-5));

        RuleFor(command => command.EndDate)
            .GreaterThan(command => command.StartDate)
            .WithMessage("End date must be after start date");

        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.TrainingId)
            .NotEqual(Guid.Empty)
            .WithMessage("Invalid trainer identifier");
    }
}
