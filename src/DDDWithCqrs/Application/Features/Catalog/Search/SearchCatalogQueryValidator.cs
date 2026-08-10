using FluentValidation;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.Search;

/// <summary>
/// Checks <see cref="SearchCatalogQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// Two rules, and both are ones the HTTP contract already carries — deliberately, for the reason
/// ADR 0046 gives: the two answer different callers. The contract answers a request and this
/// answers anything that reaches <c>IQueryDispatcher</c>, and the application layer never assumes
/// the boundary checked first.
/// <para>
/// A term longer than any title can match nothing, and a topic the domain does not spell names no
/// shelf, so refusing either says more than an empty page would — the same argument ADR 0055 makes
/// for refusing an unknown status rather than answering it with a silence.
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
    public SearchCatalogQueryValidator()
    {
        RuleFor(query => query.Term)
            .MaximumLength(MaximumTermLength)
            .WithMessage("A search term cannot be longer than a title.");

        // The domain is asked rather than a list restated, exactly as [KnownTopic] asks it at the
        // boundary (ADR 0069). Null passes — no filter is an ordinary question.
        RuleFor(query => query.Topic)
            .Must(topic => Topic.TryFromName(topic, out _))
            .When(query => query.Topic is not null)
            .WithMessage("A topic filter must name a topic this catalog knows.");

        // The boundary translates a sort name to a member and refuses the rest, but an enum is
        // only a promise at a dispatcher: any integer casts into one, so the closed set is
        // asserted here too (ADR 0046, ADR 0071).
        RuleFor(query => query.Order)
            .IsInEnum()
            .WithMessage("An order must be one the catalog publishes.");
    }
}
