using System.Linq.Expressions;
using TrainingHub.DDDWithCqrs.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Pagination;

/// <summary>
/// Turns an ordered query into one page of read models.
/// </summary>
public static class PagedQueryableExtensions
{
    /// <summary>
    /// Counts the matching rows, then fetches and projects the requested page.
    /// </summary>
    /// <remarks>
    /// It takes an <see cref="IOrderedQueryable{T}"/>, not an <see cref="IQueryable{T}"/>, and the
    /// difference is the whole safety of the thing. <c>OFFSET</c>/<c>FETCH</c> over an unordered
    /// query lets SQL Server return rows in whatever order suits it, so a row can show up on two
    /// pages and another on none — a defect that only appears under load and never reproduces.
    /// Requiring the ordered type makes that mistake impossible to write rather than something to
    /// remember.
    /// <para>
    /// Two round trips, deliberately. Counting and fetching in one statement means a window
    /// function on every row of the page, and it is the wrong trade at this size; two small,
    /// index-friendly statements are easier for the server and easier to read in a profiler. EF
    /// Core drops the ordering when translating the count, so no <c>ORDER BY</c> is paid for
    /// twice.
    /// </para>
    /// <para>
    /// The projection is applied after <c>Skip</c>/<c>Take</c> and as an expression, so SQL selects
    /// only the columns the DTO needs, for the rows of that page alone. No aggregate is
    /// materialised, and nothing is filtered or sliced in memory.
    /// </para>
    /// </remarks>
    /// <param name="source">The ordered, already filtered query.</param>
    /// <param name="projection">How a row becomes the read model — an expression EF can translate.</param>
    /// <param name="paging">The page the caller asked for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(
        this IOrderedQueryable<TSource> source,
        Expression<Func<TSource, TResult>> projection,
        PagedQuery paging,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(paging);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .Select(projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResult>(items, paging.Page, paging.PageSize, totalCount);
    }
}
