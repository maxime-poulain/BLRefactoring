using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Withhold;

/// <summary>
/// Checks <see cref="WithholdTrainingCommand"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The identifier only. The reason is judged by <c>WithholdingReason</c>, which is where that rule
/// lives (ADR 0043) and which answers with the training's own error codes; restating its bound here
/// would put one rule in two places and let them drift. What is left is the empty identifier, which
/// would reach <c>EntityId.Create</c> and throw rather than be refused by name — stated here as
/// well as on the HTTP contract because the two answer different callers, and the application layer
/// does not assume a boundary it cannot see has already checked its inputs (ADR 0046).
/// </remarks>
public sealed class WithholdTrainingCommandValidator : AbstractValidator<WithholdTrainingCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public WithholdTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId).NotEmpty();
    }
}
