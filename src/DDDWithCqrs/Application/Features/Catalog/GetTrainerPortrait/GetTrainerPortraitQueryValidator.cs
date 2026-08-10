using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTrainerPortrait;

/// <summary>
/// Checks <see cref="GetTrainerPortraitQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// Both identifiers are refused empty here as well as at the boundary, for ADR 0046's reason: the
/// route constraints answer a request, and this answers anything that reaches
/// <c>IQueryDispatcher</c>. The application layer never assumes the boundary checked first.
/// </remarks>
public sealed class GetTrainerPortraitQueryValidator : AbstractValidator<GetTrainerPortraitQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetTrainerPortraitQueryValidator()
    {
        RuleFor(query => query.TrainerId).NotEmpty();
        RuleFor(query => query.PhotoId).NotEmpty();
    }
}
