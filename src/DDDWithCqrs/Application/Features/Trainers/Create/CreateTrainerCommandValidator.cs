using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Create;

/// <summary>
/// Stands between <see cref="CreateTrainerCommand"/> and its handler, and asks nothing.
/// </summary>
/// <remarks>
/// Deliberately empty, and kept rather than deleted. Everything this command needs checking for is
/// checked by somebody better placed: the contract declares shape and presence at model binding,
/// before this pipeline runs, and the domain judges what the values mean and answers with its own
/// codes. What is left for this layer is an empty identifier that would reach
/// <c>EntityId.Create</c> and throw — and this command carries none. An empty validator states
/// that; a missing one states nothing, and the pipeline refuses a command that has none (ADR 0043).
/// </remarks>
public sealed class CreateTrainerCommandValidator : AbstractValidator<CreateTrainerCommand>
{
    /// <summary>
    /// Declares no rule.
    /// </summary>
    public CreateTrainerCommandValidator()
    {
    }
}
