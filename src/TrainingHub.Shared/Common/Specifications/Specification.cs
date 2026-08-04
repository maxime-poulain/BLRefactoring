using System.Linq.Expressions;

namespace TrainingHub.Shared.Common.Specifications;

/// <summary>
/// Base class for a named business rule over <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// The rule arrives through the constructor rather than through the protected mutator this class
/// used to offer: a specification without a rule is not a specification, and the old
/// <c>AddCriteria</c> left <c>Criteria</c> nullable — handing every consumer a question the type
/// should never have asked. A subclass is one constructor passing one expression, and that
/// expression is the whole statement of the rule.
/// </remarks>
/// <typeparam name="T">The kind of object the rule speaks about.</typeparam>
public abstract class Specification<T> : ISpecification<T> where T : class
{
    private readonly Lazy<Func<T, bool>> _predicate;

    /// <summary>
    /// Builds the specification around the rule it names.
    /// </summary>
    /// <param name="criteria">The rule, as an expression over one candidate.</param>
    protected Specification(Expression<Func<T, bool>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        Criteria = criteria;

        // Compiled once, and only on first in-memory use: compiling is the expensive step, and a
        // specification that only ever travels to the database never pays it.
        _predicate = new Lazy<Func<T, bool>>(criteria.Compile);
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>> Criteria { get; }

    /// <inheritdoc />
    public bool IsSatisfiedBy(T candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return _predicate.Value(candidate);
    }
}
