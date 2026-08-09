using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Catalogue.Search;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Validators;

/// <summary>
/// The second guard on the catalogue search's term.
/// </summary>
/// <remarks>
/// The HTTP contract bounds it at model binding, and this bounds it for anything that reaches
/// <c>IQueryDispatcher</c> by another road (ADR 0046). Not a duplicate: the application layer never
/// assumes the boundary checked first.
/// </remarks>
public sealed class SearchCatalogueQueryValidatorTests
{
    /// <summary>
    /// Validate, no term at all, accepts it.
    /// </summary>
    /// <remarks>
    /// Asking for the whole offered catalogue is the default rather than an omission to report — the
    /// same reading the administrative listings give a missing filter.
    /// </remarks>
    [Fact]
    public void Validate_NoTermAtAll_AcceptsIt() =>
        new SearchCatalogueQueryValidator()
            .Validate(new SearchCatalogueQuery())
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, a term as long as a title, accepts it.
    /// </summary>
    /// <remarks>
    /// The bound is the length of the column the term is matched against, so the longest legal title
    /// has to be a legal term: an off-by-one here would make an exact title unsearchable.
    /// </remarks>
    [Fact]
    public void Validate_ATermAsLongAsATitle_AcceptsIt() =>
        new SearchCatalogueQueryValidator()
            .Validate(new SearchCatalogueQuery
            {
                Term = new string('a', SearchCatalogueQueryValidator.MaximumTermLength)
            })
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, a term longer than a title, refuses it by naming the field.
    /// </summary>
    /// <remarks>
    /// Refused rather than answered with an empty page: a term no title can hold matches nothing,
    /// and saying so tells the caller more than a silence would (ADR 0055).
    /// </remarks>
    [Fact]
    public void Validate_ATermLongerThanATitle_RefusesItByNamingTheField()
    {
        var result = new SearchCatalogueQueryValidator()
            .Validate(new SearchCatalogueQuery
            {
                Term = new string('a', SearchCatalogueQueryValidator.MaximumTermLength + 1)
            });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(SearchCatalogueQuery.Term));
    }
}
