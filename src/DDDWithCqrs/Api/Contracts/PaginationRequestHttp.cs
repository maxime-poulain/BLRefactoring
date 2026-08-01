using System.ComponentModel.DataAnnotations;
using BLRefactoring.DDDWithCqrs.Application.Pagination;

namespace BLRefactoring.DDDWithCqrs.Api.Contracts;

/// <summary>
/// The page a caller asks for, bound from the query string.
/// </summary>
/// <remarks>
/// It sits in this host rather than in the shared contract set on purpose: only the CQRS stack
/// pages, because only it has a read side able to. Putting it among the contracts both hosts serve
/// would claim a parity that does not exist.
/// <para>
/// The bounds are data annotations, so an out-of-range page is answered at model binding with a
/// <c>ValidationProblemDetails</c> naming the offending parameter, and reaches the OpenAPI document
/// for the clients generated from it. The maximum is read from <see cref="PagedQuery.MaxPageSize"/>
/// rather than restated, so the published limit and the one the query enforces cannot drift.
/// </para>
/// </remarks>
public sealed class PaginationRequestHttp
{
    /// <summary>
    /// The page to return, counted from 1.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>
    /// How many items to return.
    /// </summary>
    [Range(1, PagedQuery.MaxPageSize)]
    public int PageSize { get; init; } = PagedQuery.DefaultPageSize;
}
