using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTrainerProfile;

/// <summary>
/// Checks <see cref="GetTrainerProfileQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The identifier is refused empty here as well as at the boundary, for ADR 0046's reason: the
/// route constraint answers a request, and this answers anything that reaches
/// <c>IQueryDispatcher</c>. The application layer never assumes the boundary checked first.
/// </remarks>
public sealed class GetTrainerProfileQueryValidator : AbstractValidator<GetTrainerProfileQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetTrainerProfileQueryValidator()
    {
        RuleFor(query => query.TrainerId).NotEmpty();
    }
}
