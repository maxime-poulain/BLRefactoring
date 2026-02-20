using BLRefactoring.Shared.Common.Specifications;

namespace BLRefactoring.Shared.Infrastructure.Specifications;

/// <summary>
/// Translates an <see cref="ISpecification{T}"/> into an Entity Framework Core <see cref="IQueryable{T}"/>
/// by applying criteria, ordering, and paging.
/// </summary>
public static class SpecificationEvaluator
{
    /// <summary>
    /// Applies the given specification to the input query, returning a new queryable with
    /// filtering, ordering, and paging applied.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="inputQuery">The base queryable to apply the specification to.</param>
    /// <param name="spec">The specification containing criteria, ordering, and paging.</param>
    /// <returns>A queryable with the specification applied.</returns>
    public static IQueryable<T> GetQuery<T>(IQueryable<T> inputQuery, ISpecification<T> spec) where T : class
    {
        var query = inputQuery;

        if (spec.Criteria is not null)
        {
            query = query.Where(spec.Criteria);
        }

        if (spec.OrderBy is not null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        else if (spec.OrderByDescending is not null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }

        if (spec.Skip.HasValue)
        {
            query = query.Skip(spec.Skip.Value);
        }

        if (spec.Take.HasValue)
        {
            query = query.Take(spec.Take.Value);
        }

        return query;
    }
}
