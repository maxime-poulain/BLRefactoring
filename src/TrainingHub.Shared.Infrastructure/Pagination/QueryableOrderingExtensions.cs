using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Infrastructure.Pagination;

/// <summary>
/// The order every paged read uses, defined once for every aggregate.
/// </summary>
/// <remarks>
/// Newest first, which is what a list of content is expected to show, and then by identifier.
/// <para>
/// That second key is not decoration, and stamping each entity from its own clock reading does not
/// make it redundant. <c>TimeProvider.GetUtcNow</c> reads the system clock, which advances at the
/// platform's timer interval — around 15.6 ms on Windows by default — so every write landing in
/// one tick carries the same <c>CreatedOn</c>, however many times the clock was read. Two
/// concurrent requests collide just as easily, and nothing makes the column unique: there is no
/// index on it, and a unique one would be worse, since it would fail concurrent inserts.
/// </para>
/// <para>
/// Ordering on <c>CreatedOn</c> alone would therefore leave tied rows in whatever order the server
/// prefers, which is enough to make a row appear on two pages or on none — a defect that passes
/// every test, never reproduces locally, and surfaces as an item missing from a list. The
/// identifier settles every remaining tie, and costs nothing: same composite <c>ORDER BY</c>, same
/// index, no extra query.
/// </para>
/// <para>
/// One definition for every aggregate — and, since both hosts page, for both of them: two list
/// endpoints ordering differently would page the same data inconsistently, and nothing would
/// report it. It is also the only way to obtain the <see cref="IOrderedQueryable{T}"/> that
/// <see cref="PagedQueryableExtensions"/> demands, so neither a query handler nor a repository
/// can page without coming through here.
/// </para>
/// <para>
/// See <c>docs/adr/0001-paginate-on-the-query-side-over-a-total-order.md</c> for the order and
/// <c>docs/adr/0029-answer-a-list-the-same-way-on-both-hosts.md</c> for why both stacks share it.
/// </para>
/// </remarks>
public static class QueryableOrderingExtensions
{
    /// <summary>
    /// Orders aggregates newest first, ties broken by identifier.
    /// </summary>
    /// <remarks>
    /// Named for what the caller gets rather than for the keys it sorts on. A mechanical name —
    /// <c>OrderByCreationDateThenById</c> — would restate the implementation in the signature, so
    /// changing the order would mean renaming every call site or leaving a name that lies. It
    /// would also read as one ordering among several, when the whole point is that there is
    /// exactly one. The tie-break is a correctness detail rather than something a caller chooses,
    /// and it is documented above instead.
    /// <para>
    /// Both type arguments are written out at the call site. <c>Id</c> is declared on
    /// <see cref="Entity{TEntityId}"/> and its type <em>is</em> that parameter, so reaching it
    /// means naming the identifier; C# infers type arguments from the arguments alone and never
    /// through constraints, which leaves <typeparamref name="TEntityId"/> with no inference
    /// source. Spelling both out is the price of one definition instead of one per aggregate.
    /// </para>
    /// </remarks>
    /// <typeparam name="TAggregate">The aggregate being listed.</typeparam>
    /// <typeparam name="TEntityId">Its identifier type.</typeparam>
    /// <param name="source">The query to order, already filtered.</param>
    public static IOrderedQueryable<TAggregate> NewestFirst<TAggregate, TEntityId>(
        this IQueryable<TAggregate> source)
        where TAggregate : AggregateRoot<TEntityId>
        where TEntityId : EntityId<TEntityId>
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .OrderByDescending(aggregate => aggregate.CreatedOn)
            .ThenBy(aggregate => aggregate.Id);
    }
}
