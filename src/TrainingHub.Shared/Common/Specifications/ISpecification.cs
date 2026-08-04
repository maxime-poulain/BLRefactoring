using System.Linq.Expressions;

namespace TrainingHub.Shared.Common.Specifications;

/// <summary>
/// A business rule about <typeparamref name="T"/>, named as a type and answerable two ways.
/// </summary>
/// <remarks>
/// One concept, two evaluations. <see cref="IsSatisfiedBy"/> answers the rule in memory, for the
/// domain or a use case deciding about an aggregate it already holds; <see cref="Criteria"/> is
/// the same rule as an expression, for a repository that has to ask the database before loading
/// anything. Keeping both on one type is what keeps the two answers from drifting apart: there is
/// exactly one statement of the rule, and both evaluations read it.
/// <para>
/// What a specification is not, in this repository: a query builder. It carries no ordering and no
/// paging (ADR 0001), and it exists only where there is a rule to name — a filter with no rule
/// content is a <c>Where</c> clause, and belongs inside the repository that runs it. See ADR 0028.
/// </para>
/// </remarks>
/// <typeparam name="T">The kind of object the rule speaks about.</typeparam>
public interface ISpecification<T> where T : class
{
    /// <summary>
    /// The rule as an expression, translatable by a persistence layer.
    /// </summary>
    Expression<Func<T, bool>> Criteria { get; }

    /// <summary>
    /// Answers the rule for one candidate, in memory.
    /// </summary>
    /// <param name="candidate">The object the rule is asked about.</param>
    /// <returns><see langword="true"/> when the candidate satisfies the rule.</returns>
    bool IsSatisfiedBy(T candidate);
}
