using System.Linq.Expressions;

namespace TrainingHub.Shared.Common.Specifications;

/// <summary>
/// Base class for implementing the Specification pattern: a named, testable filter over entities
/// of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type the specification applies to.</typeparam>
public abstract class Specification<T> : ISpecification<T> where T : class
{
    /// <inheritdoc />
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    /// <summary>
    /// Sets the filtering criteria for this specification.
    /// </summary>
    /// <param name="criteria">The expression used to filter entities.</param>
    protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;
}
