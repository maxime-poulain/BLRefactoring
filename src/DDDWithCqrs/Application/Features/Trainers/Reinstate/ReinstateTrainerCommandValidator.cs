using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Reinstate;

/// <summary>
/// Checks <see cref="ReinstateTrainerCommand"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The identifier is all this command carries, and an empty one would reach
/// <c>EntityId.Create</c> and throw rather than be refused by name. Stated here as well as on the
/// route, because the two answer different callers: the constraint answers a request, this rule
/// answers anything that reaches <c>ICommandDispatcher</c> (ADR 0046).
/// </remarks>
public sealed class ReinstateTrainerCommandValidator : AbstractValidator<ReinstateTrainerCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public ReinstateTrainerCommandValidator()
    {
        RuleFor(command => command.TrainerId).NotEmpty();
    }
}
