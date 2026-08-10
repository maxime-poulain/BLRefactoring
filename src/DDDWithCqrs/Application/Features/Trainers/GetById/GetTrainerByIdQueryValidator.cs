using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetById;

/// <summary>
/// Checks <see cref="GetTrainerByIdQuery"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behavior, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
/// <remarks>
/// The one identifier no contract could guard even in principle, which makes this rule the clearest
/// case for keeping the application layer's own gate. Every other identifier arrives in a route or
/// a body, where <c>[NotEmptyIdentifier]</c> refuses the empty one at model binding (ADR 0046).
/// This query is never built from the request: every call site hands it
/// <c>currentUserService.TrainerId</c>, read from the token, so there is no published field to
/// annotate and no boundary that could have checked it first.
/// </remarks>
public sealed class GetTrainerByIdQueryValidator : AbstractValidator<GetTrainerByIdQuery>
{
    /// <summary>
    /// Refuses an identifier the token failed to supply.
    /// </summary>
    public GetTrainerByIdQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
