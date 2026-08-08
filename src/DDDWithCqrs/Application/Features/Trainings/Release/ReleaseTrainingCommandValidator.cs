using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Release;

/// <summary>
/// Checks <see cref="ReleaseTrainingCommand"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The identifier is all this command carries, and an empty one would reach
/// <c>EntityId.Create</c> and throw rather than be refused by name. Stated here as well as on the
/// route, because the two answer different callers: the constraint answers a request, this rule
/// answers anything that reaches <c>ICommandDispatcher</c> (ADR 0046).
/// </remarks>
public sealed class ReleaseTrainingCommandValidator : AbstractValidator<ReleaseTrainingCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public ReleaseTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId).NotEmpty();
    }
}
