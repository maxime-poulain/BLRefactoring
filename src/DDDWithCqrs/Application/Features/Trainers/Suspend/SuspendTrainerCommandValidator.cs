using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Suspend;

/// <summary>
/// Checks <see cref="SuspendTrainerCommand"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The identifier only. The reason is judged by <c>SuspensionReason</c>, which is where that rule
/// lives (ADR 0043) and which answers with the trainer's own error codes; restating its bound here
/// would put one rule in two places and let them drift. What is left is the empty identifier, which
/// would reach <c>EntityId.Create</c> and throw rather than be refused by name — stated here as
/// well as on the HTTP contract because the two answer different callers, and the application layer
/// does not assume a boundary it cannot see has already checked its inputs (ADR 0046).
/// </remarks>
public sealed class SuspendTrainerCommandValidator : AbstractValidator<SuspendTrainerCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public SuspendTrainerCommandValidator()
    {
        RuleFor(command => command.TrainerId).NotEmpty();
    }
}
