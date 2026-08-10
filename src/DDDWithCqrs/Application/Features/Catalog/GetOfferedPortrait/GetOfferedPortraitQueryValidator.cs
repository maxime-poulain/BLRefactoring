using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOfferedPortrait;

/// <summary>
/// Checks <see cref="GetOfferedPortraitQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// Both identifiers are refused empty here as well as at the boundary, for ADR 0046's reason: the
/// route constraints answer a request, and this answers anything that reaches
/// <c>IQueryDispatcher</c>. The application layer never assumes the boundary checked first.
/// </remarks>
public sealed class GetOfferedPortraitQueryValidator : AbstractValidator<GetOfferedPortraitQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetOfferedPortraitQueryValidator()
    {
        RuleFor(query => query.TrainingId).NotEmpty();
        RuleFor(query => query.PhotoId).NotEmpty();
    }
}
