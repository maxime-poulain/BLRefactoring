using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOfferedPortrait;

/// <summary>
/// Checks <see cref="GetOfferedPortraitByPhotoIdQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// Both identifiers are refused empty here as well as at the boundary, for ADR 0046's reason: the
/// route constraints answer a request, and this answers anything that reaches
/// <c>IQueryDispatcher</c>. The application layer never assumes the boundary checked first.
/// </remarks>
public sealed class GetOfferedPortraitByPhotoIdQueryValidator : AbstractValidator<GetOfferedPortraitByPhotoIdQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetOfferedPortraitByPhotoIdQueryValidator()
    {
        RuleFor(query => query.TrainingId).NotEmpty();
        RuleFor(query => query.PhotoId).NotEmpty();
    }
}
