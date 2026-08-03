using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Edit;

/// <summary>
/// Guards what the caller sent. Not who they are: the trainer is no longer a field on this
/// command — the handler resolves it — so there is nothing here to check about it.
/// </summary>
public sealed class EditTrainerCommandValidator : AbstractValidator<EditTrainerCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public EditTrainerCommandValidator()
    {
        RuleFor(command => command.ContactEmail).EmailAddress();
        RuleFor(command => command.Firstname).NotEmpty();
        RuleFor(command => command.Lastname).NotEmpty();
    }
}
