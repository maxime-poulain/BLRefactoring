using System.Linq.Expressions;

namespace BLRefactoring.Shared.Common.Specifications;

/// <summary>
/// Defines a specification pattern for building composable query criteria against entities of type <typeparamref name="T"/>.
/// Specifications encapsulate query logic (filtering, ordering, paging) into reusable, testable objects.
/// </summary>
/// <typeparam name="T">The entity type the specification applies to.</typeparam>
public interface ISpecification<T> where T : class
{
    /// <summary>
    /// Gets the filtering expression used to match entities.
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>
    /// Gets the expression used to order results in ascending order.
    /// </summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>
    /// Gets the expression used to order results in descending order.
    /// </summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>
    /// Gets the maximum number of entities to return.
    /// </summary>
    int? Take { get; }

    /// <summary>
    /// Gets the number of entities to skip before returning results.
    /// </summary>
    int? Skip { get; }
}
