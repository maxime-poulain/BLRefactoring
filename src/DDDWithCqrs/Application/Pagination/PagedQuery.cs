namespace TrainingHub.DDDWithCqrs.Application.Pagination;

/// <summary>
/// The paging a query carries.
/// </summary>
/// <remarks>
/// A base class rather than a repeated pair of properties: every list query needs the same two,
/// and a query that grew its own would be the one that quietly stops honouring the maximum.
/// <para>
/// The bounds are enforced at the HTTP boundary, where a caller can be told which field is wrong;
/// they are restated here as construction defaults so that a query built in code — a test, a
/// future caller that is not a controller — is paged rather than unbounded by omission.
/// </para>
/// </remarks>
public abstract class PagedQuery
{
    /// <summary>
    /// The largest page the API will serve.
    /// </summary>
    /// <remarks>
    /// The cap is the whole point of the exercise. Without it, <c>?pageSize=1000000</c> restores
    /// precisely the unbounded read this work set out to remove.
    /// </remarks>
    public const int MaxPageSize = 100;

    /// <summary>
    /// The page size used when a caller does not ask for one.
    /// </summary>
    public const int DefaultPageSize = 20;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    /// <summary>
    /// The page to return, counted from 1.
    /// </summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    /// <summary>
    /// How many items to return, never more than <see cref="MaxPageSize"/>.
    /// </summary>
    /// <remarks>
    /// Clamped rather than rejected. A caller reaching this type has already passed the boundary,
    /// where an out-of-range value is answered with a message naming the field; what is left here
    /// is a query built in code, and for that one the cap is a server policy to apply, not an
    /// error to raise.
    /// <para>
    /// Too large is capped, but a size of zero or less falls back to <see cref="DefaultPageSize"/>
    /// rather than to one. In code that value is what an unset field looks like —
    /// <c>default(int)</c> — and answering it with a single item would be a page nobody asked for,
    /// silently, which is exactly the omission these defaults exist to cover.
    /// </para>
    /// </remarks>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }
}
