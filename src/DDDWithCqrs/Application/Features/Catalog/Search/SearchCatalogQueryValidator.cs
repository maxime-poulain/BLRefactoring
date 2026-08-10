using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.Search;

/// <summary>
/// Checks <see cref="SearchCatalogQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// One rule, and it is the one the HTTP contract already carries — deliberately, for the reason
/// ADR 0046 gives: the two answer different callers. The contract answers a request and this
/// answers anything that reaches <c>IQueryDispatcher</c>, and the application layer never assumes
/// the boundary checked first.
/// <para>
/// A term longer than any title can match nothing, so refusing it says more than an empty page
/// would — the same argument ADR 0055 makes for refusing an unknown status rather than answering
/// it with a silence.
/// </para>
/// </remarks>
public sealed class SearchCatalogQueryValidator : AbstractValidator<SearchCatalogQuery>
{
    /// <summary>
    /// The longest term worth asking about: the length of the column it is matched against.
    /// </summary>
    public const int MaximumTermLength = 100;

    /// <summary>
    /// Builds the rules.
    /// </summary>
    public SearchCatalogQueryValidator() =>
        RuleFor(query => query.Term)
            .MaximumLength(MaximumTermLength)
            .WithMessage("A search term cannot be longer than a title.");
}
