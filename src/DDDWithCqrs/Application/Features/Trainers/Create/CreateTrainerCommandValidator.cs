using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;

/// <summary>
/// Checks <see cref="CreateTrainerCommand"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behaviour, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
public sealed class CreateTrainerCommandValidator : AbstractValidator<CreateTrainerCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public CreateTrainerCommandValidator()
    {
        RuleFor(command => command.ContactEmail).EmailAddress();
        RuleFor(command => command.Firstname).NotEmpty();
        RuleFor(command => command.Lastname).NotEmpty();
    }
}
