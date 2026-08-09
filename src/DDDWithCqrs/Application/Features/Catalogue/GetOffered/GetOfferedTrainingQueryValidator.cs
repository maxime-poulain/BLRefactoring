using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalogue.GetOffered;

/// <summary>
/// Checks <see cref="GetOfferedTrainingQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The identifier is refused empty here as well as at the boundary, for ADR 0046's reason: the
/// route constraint answers a request, and this answers anything that reaches
/// <c>IQueryDispatcher</c>. The application layer never assumes the boundary checked first.
/// </remarks>
public sealed class GetOfferedTrainingQueryValidator : AbstractValidator<GetOfferedTrainingQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetOfferedTrainingQueryValidator()
    {
        RuleFor(query => query.TrainingId).NotEmpty();
    }
}
