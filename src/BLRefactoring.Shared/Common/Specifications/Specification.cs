using System.Linq.Expressions;

namespace BLRefactoring.Shared.Common.Specifications;

/// <summary>
/// Base class for implementing the Specification pattern. Provides a fluent API
/// for defining filtering criteria, ordering, and paging on entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type the specification applies to.</typeparam>
public abstract class Specification<T> : ISpecification<T> where T : class
{
    /// <inheritdoc />
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    /// <inheritdoc />
    public Expression<Func<T, object>>? OrderBy { get; private set; }

    /// <inheritdoc />
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    /// <inheritdoc />
    public int? Take { get; private set; }

    /// <inheritdoc />
    public int? Skip { get; private set; }

    /// <summary>
    /// Sets the filtering criteria for this specification.
    /// </summary>
    /// <param name="criteria">The expression used to filter entities.</param>
    protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    /// <summary>
    /// Sets ascending ordering for this specification.
    /// </summary>
    /// <param name="orderBy">The expression used to order entities in ascending order.</param>
    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    /// <summary>
    /// Sets descending ordering for this specification.
    /// </summary>
    /// <param name="orderByDescending">The expression used to order entities in descending order.</param>
    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) =>
        OrderByDescending = orderByDescending;

    /// <summary>
    /// Applies paging to this specification.
    /// </summary>
    /// <param name="skip">The number of entities to skip.</param>
    /// <param name="take">The maximum number of entities to return.</param>
    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }
}
