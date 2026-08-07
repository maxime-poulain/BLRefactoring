using System.ComponentModel.DataAnnotations;
using TrainingHub.Shared.Common.Pagination;

namespace TrainingHub.Shared.Api.Contracts.Pagination;

/// <summary>
/// The page a caller asks for, bound from the query string.
/// </summary>
/// <remarks>
/// It sits among the contracts both hosts serve because both hosts page. It used to live in the
/// CQRS host alone, and its own remark said why: only that stack paged, and placing it here would
/// have claimed a parity that did not exist. ADR 0029 made the parity real, and the contract moved
/// the day the claim became true.
/// <para>
/// The bounds are data annotations, so an out-of-range page is answered at model binding with a
/// <c>ValidationProblemDetails</c> naming the offending parameter, and reaches the OpenAPI document
/// for the clients generated from it. The maximum is read from <see cref="PageRequest.MaxPageSize"/>
/// rather than restated, so the published limit and the one the server enforces cannot drift.
/// </para>
/// </remarks>
public sealed class PaginationHttpRequest
{
    /// <summary>
    /// The page to return, counted from 1.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>
    /// How many items to return.
    /// </summary>
    [Range(1, PageRequest.MaxPageSize)]
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}
