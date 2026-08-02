using BLRefactoring.Shared.Common.Specifications;

namespace BLRefactoring.Shared.Infrastructure.Specifications;

/// <summary>
/// Translates an <see cref="ISpecification{T}"/> into an Entity Framework Core
/// <see cref="IQueryable{T}"/>.
/// </summary>
/// <remarks>
/// Criteria and nothing else. It used to apply ordering and paging too, from four specification
/// properties nothing ever set — so the four branches were unreachable, and the ordering they
/// would have applied belongs to the query side anyway. See ADR 0001.
/// </remarks>
public static class SpecificationEvaluator
{
    /// <summary>
    /// Applies the given specification's criteria to the input query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="inputQuery">The base queryable to apply the specification to.</param>
    /// <param name="spec">The specification containing the criteria.</param>
    /// <returns>A queryable with the specification applied.</returns>
    public static IQueryable<T> GetQuery<T>(IQueryable<T> inputQuery, ISpecification<T> spec) where T : class
    {
        ArgumentNullException.ThrowIfNull(spec);

        return spec.Criteria is null ? inputQuery : inputQuery.Where(spec.Criteria);
    }
}
