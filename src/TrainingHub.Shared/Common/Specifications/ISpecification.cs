using System.Linq.Expressions;

namespace TrainingHub.Shared.Common.Specifications;

/// <summary>
/// A reusable, testable piece of query criteria against entities of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// Filtering only. It used to declare ordering and paging as well — <c>OrderBy</c>,
/// <c>OrderByDescending</c>, <c>Take</c>, <c>Skip</c> — and no specification in the solution ever
/// set one, because ADR 0001 puts paging on the query side and out of the repositories entirely:
/// "no use case in this domain loads aggregates by the page". Four properties that could only ever
/// be null, and four branches in the evaluator that could only ever be skipped, read as a
/// capability rather than as the leftover they were.
/// </remarks>
/// <typeparam name="T">The entity type the specification applies to.</typeparam>
public interface ISpecification<T> where T : class
{
    /// <summary>
    /// Gets the filtering expression used to match entities.
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }
}
