using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTopics;

/// <summary>
/// Refuses a term the facets could not be counted under.
/// </summary>
/// <remarks>
/// This query had no validator while it carried nothing, and the record said so in as many words.
/// It carries a term now (ADR 0080), so it has one — and the bound is the search's own, read from
/// <c>SearchCatalogQueryValidator</c> rather than written again, because the two endpoints
/// are answering the same question and a term either endpoint refuses is a term both must.
/// <para>
/// One validator per message, beside it, and the application layer never assumes the boundary
/// checked first: <c>[StringLength]</c> answers a request, this answers anything that reaches a
/// dispatcher (ADR 0046).
/// </para>
/// </remarks>
public sealed class GetCatalogTopicsByTermQueryValidator : AbstractValidator<GetCatalogTopicsByTermQuery>
{
    /// <summary>
    /// Initializes the rules.
    /// </summary>
    public GetCatalogTopicsByTermQueryValidator() =>
        RuleFor(query => query.Term)
            .MaximumLength(Search.SearchCatalogQueryValidator.MaximumTermLength)
            .WithMessage("A search term cannot be longer than a title.");
}
